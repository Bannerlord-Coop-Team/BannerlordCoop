using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Handlers;

internal class PartyDoneLogicHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<PartyDoneLogicHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public PartyDoneLogicHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<AttemptPartyDoneLogic>(Handle_AttemptPartyDoneLogic);
        messageBroker.Subscribe<CompletePartyDoneLogic>(Handle_CompletePartyDoneLogic);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AttemptPartyDoneLogic>(Handle_AttemptPartyDoneLogic);
        messageBroker.Unsubscribe<CompletePartyDoneLogic>(Handle_CompletePartyDoneLogic);
    }

    private void Handle_AttemptPartyDoneLogic(MessagePayload<AttemptPartyDoneLogic> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        string leftPartyId = null;
        if (obj.What.LeftParty != null && !objectManager.TryGetIdWithLogging(obj.What.LeftParty, out leftPartyId)) return;

        List<Tuple<string, string, int>> upgradedTroopHistoryIds = new();
        foreach (Tuple<CharacterObject, CharacterObject, int> tuple in obj.What.UpgradedTroopHistory)
        {
            if (!objectManager.TryGetIdWithLogging(tuple.Item1, out var character1Id)) continue;
            if (!objectManager.TryGetIdWithLogging(tuple.Item2, out var character2Id)) continue;

            upgradedTroopHistoryIds.Add(new(character1Id, character2Id, tuple.Item3));
        }

        var leftMemberRosterData = troopRosterInterface.PackTroopRosterData(obj.What.LeftMemberRoster);
        var leftPrisonerRosterData = troopRosterInterface.PackTroopRosterData(obj.What.LeftPrisonerRoster);
        var rightMemberRosterData = troopRosterInterface.PackTroopRosterData(obj.What.RightMemberRoster);
        var rightPrisonerRosterData = troopRosterInterface.PackTroopRosterData(obj.What.RightPrisonerRoster);

        var message = new CompletePartyDoneLogic(
            mainHeroId,
            FlattenedTroopSerializer.Serialize(obj.What.TakenPrisonersRoster, objectManager),
            FlattenedTroopSerializer.Serialize(obj.What.DonatedPrisonersRoster, objectManager),
            FlattenedTroopSerializer.Serialize(obj.What.RecruitedPrisonersRoster, objectManager),
            leftMemberRosterData,
            leftPrisonerRosterData,
            rightMemberRosterData,
            rightPrisonerRosterData,
            obj.What.RightOwnerPartyItemRoster._data,
            upgradedTroopHistoryIds,
            leftPartyId,
            obj.What.PartyGoldChangeAmount,
            obj.What.PartyInfluenceChangeAmount,
            obj.What.PartyMoraleChangeAmount,
            obj.What.DoNotApplyGoldTransactions
        );

        network.SendAll(message);
    }

    private void Handle_CompletePartyDoneLogic(MessagePayload<CompletePartyDoneLogic> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;

        PartyBase leftParty = null;
        if (obj.What.LeftPartyId != null && !objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.LeftPartyId, out leftParty)) return;

        List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory = new();
        if (obj.What.UpgradedTroopHistoryIds != null)
        {
            foreach (Tuple<string, string, int> tuple in obj.What.UpgradedTroopHistoryIds)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(tuple.Item1, out var character1)) continue;
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(tuple.Item2, out var character2)) continue;

                upgradedTroopHistory.Add(new(character1, character2, tuple.Item3));
            }
        }

        var donatedPrisonersRoster = FlattenedTroopSerializer.Deserialize(obj.What.DonatedPrisonersRoster, objectManager);

        if (leftParty != null)
        {
            troopRosterInterface.UpdateWithData(leftParty.MemberRoster, obj.What.LeftMemberRosterData, mainHero);
            troopRosterInterface.UpdateWithData(leftParty.PrisonRoster, obj.What.LeftPrisonerRosterData, mainHero);
        }

        troopRosterInterface.UpdateWithData(mainHero.PartyBelongedTo.MemberRoster, obj.What.RightMemberRosterData, mainHero);
        troopRosterInterface.UpdateWithData(mainHero.PartyBelongedTo.PrisonRoster, obj.What.RightPrisonerRosterData, mainHero);

        mainHero.PartyBelongedTo.ItemRoster.Clear();
        foreach (var itemRosterElement in obj.What.RightOwnerPartyItemRosterData ?? Enumerable.Empty<ItemRosterElement>())
        {
            mainHero.PartyBelongedTo.ItemRoster.Add(itemRosterElement);
        }

        if (Settlement.CurrentSettlement != null && !donatedPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>())
        {
            CampaignEventDispatcher.Instance.OnPrisonersChangeInSettlement(Settlement.CurrentSettlement, donatedPrisonersRoster, null, true);
        }
        if (!obj.What.DoNotApplyGoldTransactions)
        {
            GiveGoldAction.ApplyBetweenCharacters(null, mainHero, obj.What.PartyGoldChangeAmount, false);
            network.Send(obj.Who as NetPeer, new NotifyGoldChange(obj.What.PartyGoldChangeAmount));
        }
        if (obj.What.PartyInfluenceChangeAmount != 0)
        {
            // TODO
            GainKingdomInfluenceAction.ApplyForLeavingTroopToGarrison(Hero.MainHero, (float)obj.What.PartyInfluenceChangeAmount);
        }

        //Replacement for CampaignEventDispatcher.Instance.OnPlayerUpgradedTroops(tuple.Item1, tuple.Item2, tuple.Item3) without MainParty
        foreach (Tuple<CharacterObject, CharacterObject, int> tuple in upgradedTroopHistory)
        {
            SkillLevelingManager.OnUpgradeTroops(mainHero.PartyBelongedTo.Party, tuple.Item1, tuple.Item2, tuple.Item3);
        }

        if (obj.What.RecruitedPrisonersRoster != null && !donatedPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>())
        {
            // Replacement for CampaignEventDispatcher.Instance.OnMainPartyPrisonerRecruited(obj.What.RecruitedPrisonersRoster);
            foreach (CharacterObject characterObject in donatedPrisonersRoster.Troops)
            {
                // Replace CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
                if (mainHero.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
                {
                    mainHero.PartyBelongedTo.MemberRoster.AddXpToTroop(characterObject, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * 1);
                }
                SkillLevelingManager.OnTroopRecruited(mainHero, 1, characterObject.Tier);
                if (characterObject.Occupation == Occupation.Bandit)
                {
                    SkillLevelingManager.OnBanditsRecruited(mainHero.PartyBelongedTo, characterObject, 1);
                }

                // Replace ApplyPrisonerRecruitmentEffects
                int prisonerRecruitmentMoraleEffect = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect(mainHero.PartyBelongedTo.Party, characterObject, 1);
                mainHero.PartyBelongedTo.RecentEventsMorale += (float)prisonerRecruitmentMoraleEffect;
            }
        }
    }
}