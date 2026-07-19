using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
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

        messageBroker.Subscribe<PartyDoneLogicAttempted>(Handle_PartyDoneLogicAttempted);
        messageBroker.Subscribe<NetworkCompleteDoneLogic>(Handle_CompletePartyDoneLogic);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyDoneLogicAttempted>(Handle_PartyDoneLogicAttempted);
        messageBroker.Unsubscribe<NetworkCompleteDoneLogic>(Handle_CompletePartyDoneLogic);
    }

    // Client
    private void Handle_PartyDoneLogicAttempted(MessagePayload<PartyDoneLogicAttempted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        string leftPartyId = null;
        if (obj.What.LeftParty != null && 
            !objectManager.TryGetIdWithLogging(obj.What.LeftParty, out leftPartyId))
            return;

        // Not registered when donating
        objectManager.TryGetId(obj.What.LeftPrisonerRoster, out var leftPrisonerRosterId);

        var upgradedTroopHistory = new UpgradedTroopHistoryData(new());
        foreach (Tuple<CharacterObject, CharacterObject, int> tuple in obj.What.UpgradedTroopHistory)
        {
            if (!objectManager.TryGetIdWithLogging(tuple.Item1, out var character1Id)) continue;
            if (!objectManager.TryGetIdWithLogging(tuple.Item2, out var character2Id)) continue;

            upgradedTroopHistory.Data.Add(new(character1Id, character2Id, tuple.Item3));
        }

        // Send only the per-troop change the player made (current minus the screen-open snapshot). Heroes and
        // companions that did not change net to zero and are omitted, so the server needs no special handling
        // for them when re-applying the delta.
        var leftMemberRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.LeftMemberRoster, obj.What.InitialLeftMemberRoster);
        var leftPrisonerRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.LeftPrisonerRoster, obj.What.InitialLeftPrisonerRoster);
        var rightMemberRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.RightMemberRoster, obj.What.InitialRightMemberRoster);
        var rightPrisonerRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.RightPrisonerRoster, obj.What.InitialRightPrisonerRoster);

        var rightMemberOrderData = troopRosterInterface.PackTroopRosterOrderData(obj.What.RightMemberRoster);

        var releaserPartyPosition = GetReleaserPartyPosition(obj.What.MainHero);

        var message = new NetworkCompleteDoneLogic(
            mainHeroId,
            FlattenedTroopSerializer.Serialize(obj.What.TakenPrisonersRoster, objectManager),
            FlattenedTroopSerializer.Serialize(obj.What.DonatedPrisonersRoster, objectManager),
            FlattenedTroopSerializer.Serialize(obj.What.RecruitedPrisonersRoster, objectManager),
            leftMemberRosterData,
            leftPrisonerRosterData,
            rightMemberRosterData,
            rightPrisonerRosterData,
            obj.What.RightOwnerPartyItemRoster._data,
            upgradedTroopHistory,
            leftPartyId,
            leftPrisonerRosterId,
            obj.What.PartyGoldChangeAmount,
            obj.What.PartyInfluenceChangeAmount,
            obj.What.PartyMoraleChangeAmount,
            obj.What.DoNotApplyGoldTransactions,
            releaserPartyPosition,
            obj.What.PartyScreenMode,
            rightMemberOrderData
        );

        network.SendAll(message);
    }

    private static CampaignVec2 GetReleaserPartyPosition(Hero mainHero)
    {
        var releaserParty = mainHero.PartyBelongedTo;
        if (releaserParty?.CurrentSettlement != null)
            return releaserParty.CurrentSettlement.GatePosition;

        if (releaserParty != null)
            return releaserParty.Position;

        return MobileParty.MainParty.Position;
    }

    // Server
    private void Handle_CompletePartyDoneLogic(MessagePayload<NetworkCompleteDoneLogic> obj)
    {
        var message = obj.What;
        
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(message.MainHeroId, out var mainHero)) return;

            if (!TryResolveCompleteDoneLogic(message, out var leftParty, out var leftPrisonerRoster, out var upgradedTroopHistory)) return;

            var donatedPrisonersRoster = FlattenedTroopSerializer.Deserialize(message.DonatedPrisonersRoster, objectManager);
            var recruitedPrisonersRoster = FlattenedTroopSerializer.Deserialize(message.RecruitedPrisonersRoster, objectManager);
            var releasedPlayerCaptivityEvents = CreatePlayerCaptivityReleaseEvents(
                message.LeftPrisonerRosterData,
                message.RightPrisonerRosterData,
                message.ReleaserPartyPosition,
                out var leftPrisonerRosterData,
                out var rightPrisonerRosterData);

            var rosterDeltas = CreateRosterDeltas(
                mainHero,
                leftParty,
                leftPrisonerRoster,
                message,
                leftPrisonerRosterData,
                rightPrisonerRosterData);

            PublishPlayerCaptivityReleaseEvents(releasedPlayerCaptivityEvents);

            // Only apply deltas if not ransoming. SellPrisonersAction already changes troop rosters
            if (message.PartyScreenMode != Helpers.PartyScreenHelper.PartyScreenMode.Ransom)
            {
                troopRosterInterface.ApplyTroopRosterDeltas(rosterDeltas);
            }
            ApplyRightOwnerPartyItemRoster(mainHero, message);
            NotifyDonatedPrisonersChanged(donatedPrisonersRoster);
            ApplyPartyRewardChanges(mainHero, message);
            ApplyUpgradedTroopHistory(mainHero, upgradedTroopHistory);
            ApplyPrisonerRecruitmentEffects(mainHero, message, recruitedPrisonersRoster);

            ApplyRosterOrder(mainHero.PartyBelongedTo.MemberRoster, message.RightMemberOrderData);
        });
    }

    private bool TryResolveCompleteDoneLogic(
        NetworkCompleteDoneLogic message,
        out PartyBase leftParty,
        out TroopRoster leftPrisonerRoster,
        out List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory)
    {
        leftParty = null;
        leftPrisonerRoster = null;
        upgradedTroopHistory = null;

        if (message.LeftPartyId != null && !objectManager.TryGetObjectWithLogging<PartyBase>(message.LeftPartyId, out leftParty)) return false;
        if (message.LeftPrisonerRosterId != null && !objectManager.TryGetObjectWithLogging<TroopRoster>(message.LeftPrisonerRosterId, out leftPrisonerRoster)) return false;

        upgradedTroopHistory = ResolveUpgradedTroopHistory(message.UpgradedTroopHistoryIds);
        return true;
    }

    private List<Tuple<CharacterObject, CharacterObject, int>> ResolveUpgradedTroopHistory(UpgradedTroopHistoryData upgradedTroopHistoryIds)
    {
        List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory = new();
        if (upgradedTroopHistoryIds.Data == null) return upgradedTroopHistory;

        foreach (var elementData in upgradedTroopHistoryIds.Data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.Character1Id, out var character1)) continue;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.Character2Id, out var character2)) continue;

            upgradedTroopHistory.Add(new(character1, character2, elementData.Number));
        }

        return upgradedTroopHistory;
    }

    private static List<(TroopRoster roster, TroopRosterData delta)> CreateRosterDeltas(
        Hero mainHero,
        PartyBase leftParty,
        TroopRoster leftPrisonerRoster,
        NetworkCompleteDoneLogic message,
        TroopRosterData leftPrisonerRosterData,
        TroopRosterData rightPrisonerRosterData)
    {
        // Collect every roster delta and apply them together: ApplyTroopRosterDeltas removes before it
        // adds across all rosters, so a hero/prisoner moved between parties keeps its party linkage
        // (the destination addition is the last AddToCounts on that hero).
        var rosterDeltas = new List<(TroopRoster roster, TroopRosterData delta)>();
        if (leftParty != null)
        {
            rosterDeltas.Add((leftParty.MemberRoster, message.LeftMemberRosterData));
            rosterDeltas.Add((leftParty.PrisonRoster, leftPrisonerRosterData));
        }
        else if (leftPrisonerRoster != null) // Prisoner management doesn't have a set party
        {
            rosterDeltas.Add((leftPrisonerRoster, leftPrisonerRosterData));
        }

        rosterDeltas.Add((mainHero.PartyBelongedTo.MemberRoster, message.RightMemberRosterData));
        rosterDeltas.Add((mainHero.PartyBelongedTo.PrisonRoster, rightPrisonerRosterData));
        return rosterDeltas;
    }

    private void PublishPlayerCaptivityReleaseEvents(List<PlayerCaptivityEndedByServer> releasedPlayerCaptivityEvents)
    {
        foreach (var releaseEvent in releasedPlayerCaptivityEvents)
        {
            messageBroker.Publish(this, releaseEvent);
        }
    }

    private static void ApplyRightOwnerPartyItemRoster(Hero mainHero, NetworkCompleteDoneLogic message)
    {
        mainHero.PartyBelongedTo.ItemRoster.Clear();
        foreach (var itemRosterElement in message.RightOwnerPartyItemRosterData ?? Enumerable.Empty<ItemRosterElement>())
        {
            mainHero.PartyBelongedTo.ItemRoster.Add(itemRosterElement);
        }
    }

    private static void NotifyDonatedPrisonersChanged(FlattenedTroopRoster donatedPrisonersRoster)
    {
        if (Settlement.CurrentSettlement == null) return;
        if (donatedPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>()) return;

        CampaignEventDispatcher.Instance.OnPrisonersChangeInSettlement(Settlement.CurrentSettlement, donatedPrisonersRoster, null, true);
    }

    private static void ApplyPartyRewardChanges(Hero mainHero, NetworkCompleteDoneLogic message)
    {
        if (!message.DoNotApplyGoldTransactions)
        {
            GiveGoldAction.ApplyBetweenCharacters(null, mainHero, message.PartyGoldChangeAmount, false);
        }
        if (message.PartyInfluenceChangeAmount != 0)
        {
            // Influence goes to the requesting player's clan (mainHero), not the local machine's
            // Hero.MainHero - which is null on a dedicated server (NRE) and the wrong clan otherwise.
            GainKingdomInfluenceAction.ApplyForLeavingTroopToGarrison(mainHero, (float)message.PartyInfluenceChangeAmount);
        }
    }

    private static void ApplyUpgradedTroopHistory(Hero mainHero, List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory)
    {
        //Replacement for CampaignEventDispatcher.Instance.OnPlayerUpgradedTroops(tuple.Item1, tuple.Item2, tuple.Item3) without MainParty
        foreach (Tuple<CharacterObject, CharacterObject, int> tuple in upgradedTroopHistory)
        {
            SkillLevelingManager.OnUpgradeTroops(mainHero.PartyBelongedTo.Party, tuple.Item1, tuple.Item2, tuple.Item3);
        }
    }

    private static void ApplyPrisonerRecruitmentEffects(
        Hero mainHero,
        NetworkCompleteDoneLogic message,
        FlattenedTroopRoster recruitedPrisonersRoster)
    {
        if (message.RecruitedPrisonersRoster == null) return;
        if (recruitedPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>()) return;

        // Replacement for CampaignEventDispatcher.Instance.OnMainPartyPrisonerRecruited(obj.What.RecruitedPrisonersRoster);
        foreach (CharacterObject characterObject in recruitedPrisonersRoster.Troops)
        {
            ApplyPrisonerRecruitmentEffect(mainHero, characterObject);
        }
    }

    private static void ApplyPrisonerRecruitmentEffect(Hero mainHero, CharacterObject characterObject)
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

    private void ApplyRosterOrder(TroopRoster roster, TroopRosterOrderData orderData)
    {
        messageBroker.Publish(this, new ApplyTroopRosterOrder(roster, orderData));
    }

    internal List<PlayerCaptivityEndedByServer> CreatePlayerCaptivityReleaseEvents(
        TroopRosterData leftPrisonerRosterData,
        TroopRosterData rightPrisonerRosterData,
        CampaignVec2 releaserPartyPosition,
        out TroopRosterData filteredLeftPrisonerRosterData,
        out TroopRosterData filteredRightPrisonerRosterData)
    {
        var releasedPlayerPrisoners = new List<Hero>();
        var transferredPlayerPrisoners = GetTransferredPlayerPrisoners(leftPrisonerRosterData, rightPrisonerRosterData);
        filteredLeftPrisonerRosterData = FilterPlayerPrisonerReleaseDelta(leftPrisonerRosterData, transferredPlayerPrisoners, releasedPlayerPrisoners);
        filteredRightPrisonerRosterData = FilterPlayerPrisonerReleaseDelta(rightPrisonerRosterData, transferredPlayerPrisoners, releasedPlayerPrisoners);

        return releasedPlayerPrisoners
            .Select(playerHero => new PlayerCaptivityEndedByServer(playerHero, EndCaptivityDetail.ReleasedByChoice, null, releaserPartyPosition))
            .ToList();
    }

    private HashSet<string> GetTransferredPlayerPrisoners(params TroopRosterData[] prisonerRosterDeltas)
    {
        var transferredPlayerPrisoners = new HashSet<string>();
        foreach (var delta in prisonerRosterDeltas)
        {
            foreach (var elementData in delta.Data ?? Array.Empty<TroopRosterElementData>())
            {
                if (elementData.Number > 0 && TryGetPlayerPrisonerHero(elementData, out _))
                    transferredPlayerPrisoners.Add(elementData.CharacterId);
            }
        }

        return transferredPlayerPrisoners;
    }

    private TroopRosterData FilterPlayerPrisonerReleaseDelta(
        TroopRosterData delta,
        HashSet<string> transferredPlayerPrisoners,
        List<Hero> releasedPlayerPrisoners)
    {
        if (delta.Data == null) return delta;

        var filtered = new List<TroopRosterElementData>();
        foreach (var elementData in delta.Data)
        {
            if (elementData.Number < 0 &&
                TryGetPlayerPrisonerHero(elementData, out var playerHero) &&
                !transferredPlayerPrisoners.Contains(elementData.CharacterId))
            {
                releasedPlayerPrisoners.Add(playerHero);
                continue;
            }

            filtered.Add(elementData);
        }

        return filtered.Count == delta.Data.Length
            ? delta
            : new TroopRosterData(filtered);
    }

    private bool TryGetPlayerPrisonerHero(TroopRosterElementData elementData, out Hero playerHero)
    {
        playerHero = null;
        return objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character) &&
               (playerHero = character.HeroObject)?.IsPlayerHero() == true;
    }
}
