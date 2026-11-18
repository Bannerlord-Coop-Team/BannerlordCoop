using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using Helpers;
using System.Linq;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal interface IExtortionByDesertersWorldEventService
{
    bool TryStartAmbush(Quest quest);
    bool TryCleanupBattle(Quest quest);
    void StartAmbushEncounter(Quest quest);
    void ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender);
    void ApplySettlementLeft(Quest quest, MobileParty party, Settlement settlement);
    void ApplyMapEventEnded(Quest quest, MapEvent mapEvent);
    void ApplyVillageBeingRaided(Quest quest, Village village);
    void ApplyTick(Quest quest);
}

internal sealed class ExtortionByDesertersWorldEventService : IExtortionByDesertersWorldEventService
{
    private readonly IExtortionByDesertersProgress progress;
    private readonly IIssueOwnerParty ownerParty;

    public ExtortionByDesertersWorldEventService(IExtortionByDesertersProgress progress, IIssueOwnerParty ownerParty)
    {
        this.progress = progress;
        this.ownerParty = ownerParty;
    }

    public bool TryStartAmbush(Quest quest)
    {
        var party = quest._deserterMobileParty;
        var raid = party?.MapEvent;
        var ownerParty = Hero.MainHero.PartyBelongedTo;
        var isQuestRaid = raid?.IsRaid == true && !raid.HasWinner && !raid.IsFinalized &&
            raid.AttackerSide.LeaderParty == party?.Party &&
            raid.DefenderSide.LeaderParty == quest.QuestSettlement.Party;
        var ownerCanDefend = ownerParty?.CurrentSettlement == quest.QuestSettlement &&
            (ownerParty.MapEvent == null || ownerParty.MapEvent == raid);
        if (!isQuestRaid || !ownerCanDefend || quest._defenderMobileParty != null)
        {
            return false;
        }

        quest.CreateDefenderParty();
        quest._defenderMobileParty.MapEventSide = raid.DefenderSide;
        ownerParty.MapEventSide = raid.DefenderSide;
        party.IgnoreByOtherPartiesTill(CampaignTime.Now - CampaignTime.Hours(1f));
        if (quest.QuestSettlement.MilitiaPartyComponent != null)
        {
            quest.QuestSettlement.MilitiaPartyComponent.MobileParty.MapEventSide = null;
        }

        progress.Send(quest, notice: ExtortionByDesertersNotice.Ambush);
        return true;
    }

    public void StartAmbushEncounter(Quest quest)
    {
        var raid = quest._deserterMobileParty?.MapEvent;
        if (ModInformation.IsServer || raid == null || quest._defenderMobileParty == null ||
            MobileParty.MainParty.MapEvent != raid) return;

        using (new AllowedThread())
        {
            if (PlayerEncounter.Battle != raid)
            {
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Current._mapEvent = null;
                    PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
                }
                PlayerEncounter.Start();
            }

            PlayerEncounter.Init();
            var leader = ConversationHelper.GetConversationCharacterPartyLeader(quest._deserterMobileParty.Party);
            var player = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true);
            var deserter = new ConversationCharacterData(leader, quest._deserterMobileParty.Party, noHorse: true);
            GameMenu.ActivateGameMenu("encounter_meeting");
            CampaignMapConversation.OpenConversation(player, deserter);
        }
    }

    public bool TryCleanupBattle(Quest quest)
    {
        if (!quest._deserterBattleFinalizedForTheFirstTime)
        {
            if (quest.IsOngoing && quest._deserterMobileParty?.IsActive == false)
            {
                quest.CompleteQuestWithCancel();
            }
            return false;
        }

        quest._deserterBattleFinalizedForTheFirstTime = false;
        if (quest._currentState == State.DesertersAreDefeated)
        {
            quest.DestroyDeserterParty();
            quest._deserterMobileParty = null;
        }

        quest.DestroyDefenderParty();
        quest._defenderMobileParty = null;
        progress.Send(quest);
        return true;
    }

    public void ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (ModInformation.IsClient || Hero.MainHero.PartyBelongedTo == null) return;
        if (attacker == PartyBase.MainParty)
        {
            if (mapEvent.IsFieldBattle && defender.IsMobile &&
                defender.MobileParty.HomeSettlement == quest.QuestSettlement &&
                defender != quest._deserterMobileParty?.Party)
            {
                quest.CompleteQuestWithFail(quest.OnQuestFailed3LogText);
                quest.ApplyQuestResult(quest._questResultFail3);
            }
            else if (QuestHelper.CheckMinorMajorCoercion(quest, mapEvent, attacker))
            {
                QuestHelper.ApplyGenericMinorMajorCoercionConsequences(quest, mapEvent);
            }
        }

    }

    public void ApplySettlementLeft(Quest quest, MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsClient || Hero.MainHero.PartyBelongedTo == null) return;
        var deserters = quest._deserterMobileParty;
        var raid = settlement.Party.MapEvent;
        if (party.IsMainParty && settlement == quest.QuestSettlement && deserters != null &&
            raid?.IsRaid == true && raid.AttackerSide.LeaderParty == deserters.Party)
        {
            quest.ApplyQuestResult(quest._questResultFail2);
            quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
        }

        if (quest.IsOngoing && party == Hero.MainHero.PartyBelongedTo && settlement == quest.QuestSettlement)
        {
            quest.TickDesertersPartyLogic();
        }
    }

    public void ApplyMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        var deserters = quest._deserterMobileParty;
        if (ModInformation.IsClient || deserters == null || !mapEvent.InvolvedParties.Contains(deserters.Party)) return;
        if (!ownerParty.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return;
        if (!mapEvent.InvolvedParties.Contains(owner.Party)) return;

        quest._deserterBattleFinalizedForTheFirstTime = true;
        if (mapEvent.WinningSide == owner.Party.Side)
        {
            quest._currentState = State.DesertersAreDefeated;
            quest.AddLog(quest.OnDeserterPartyDefeatedLogText);
            progress.Send(quest, notice: ExtortionByDesertersNotice.DesertersDefeated);
        }
        else
        {
            SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
            quest._currentState = State.DesertersDefeatedPlayer;
            progress.Send(quest);
        }

    }

    public void ApplyVillageBeingRaided(Quest quest, Village village)
    {
        if (ModInformation.IsClient || village.Settlement != quest.QuestSettlement) return;
        var attacker = village.Settlement.Party.MapEvent?.AttackerSide.LeaderParty;
        if (attacker == null) return;
        if (attacker == quest._deserterMobileParty?.Party)
        {
            // Unlike vanilla's PlayerEncounter check, use the owner's substituted party.
            if (Hero.MainHero.PartyBelongedTo?.CurrentSettlement == village.Settlement) return;
            quest.ApplyQuestResult(quest._questResultFail2);
            quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
        }
        else if (attacker == Hero.MainHero.PartyBelongedTo?.Party)
        {
            quest.ApplyQuestResult(quest._questResultFail3);
            quest.CompleteQuestWithFail(quest.OnQuestFailed3LogText);
        }
        else
        {
            // Vanilla applies an all-zero cancel result here. The branch has no consequences to apply.
            quest.CompleteQuestWithCancel(quest.OnQuestCancel2LogText);
        }
    }

    public void ApplyTick(Quest quest)
    {
        if (ModInformation.IsClient || !quest.IsOngoing || quest._currentState == State.DesertersAreDefeated) return;
        var deserters = quest._deserterMobileParty;
        if (deserters == null || !deserters.IsActive)
        {
            quest.CompleteQuestWithCancel();
            return;
        }
        if (deserters.MapEvent != null)
        {
            TryStartAmbush(quest);
            return;
        }

        if (quest._currentState == State.DesertersDefeatedPlayer)
        {
            SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
            return;
        }

        var owner = Hero.MainHero.PartyBelongedTo;
        if (owner == null) return;
        switch (quest._currentState)
        {
            case State.DesertersMovingToSettlement:
                if (owner.CurrentSettlement == quest.QuestSettlement)
                {
                    SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
                }
                else if (deserters.Position.Distance(owner.Position) <= deserters.SeeingRange * 0.8f)
                {
                    quest.HandleDesertersRunningAway();
                    quest._currentState = State.DesertersRunningAwayFromPlayer;
                    quest._desertersRunAwayTimeoutTime = CampaignTime.HoursFromNow(10f);
                    progress.Send(quest, notice: ExtortionByDesertersNotice.DesertersNoticedPlayer);
                }
                else if (!quest._playerAwayFromSettlementNotificationSent)
                {
                    quest._playerAwayFromSettlementNotificationSent = true;
                    progress.Send(quest, notice: ExtortionByDesertersNotice.PlayerLeftVillage);
                }
                break;
            case State.DesertersRunningAwayFromPlayer:
                if (deserters.Position.Distance(owner.Position) > owner.SeeingRange + 3f)
                {
                    quest.ApplyQuestResult(quest._questResultFail1);
                    quest.CompleteQuestWithFail(quest.OnQuestFailed1LogText);
                }
                else if (quest._desertersRunAwayTimeoutTime.IsPast)
                {
                    quest.DestroyDeserterParty();
                    quest.ApplyQuestResult(quest._questResultFail1);
                    quest.CompleteQuestWithFail(quest.OnQuestFailed1LogText);
                }
                else quest.HandleDesertersRunningAway();
                break;
        }
    }

    internal static void SetRaidBehaviorIfNeeded(MobileParty deserters, Settlement settlement)
    {
        if (deserters.DefaultBehavior == AiBehavior.RaidSettlement) return;
        SetPartyAiAction.GetActionForRaidingSettlement(deserters, settlement, MobileParty.NavigationType.Default, false, false);
    }
}
