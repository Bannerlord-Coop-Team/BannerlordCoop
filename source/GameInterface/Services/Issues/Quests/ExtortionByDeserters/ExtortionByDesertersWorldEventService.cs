using Common;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Handlers;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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
    bool ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender);
    bool ApplySettlementLeft(Quest quest, MobileParty party, Settlement settlement);
    bool ApplyMapEventEnded(Quest quest, MapEvent mapEvent);
    bool ApplyVillageBeingRaided(Quest quest, Village village);
    bool ApplyTick(Quest quest);
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
        if (ownerParty == null || raid?.IsRaid != true || raid.HasWinner || raid.IsFinalized ||
            raid.AttackerSide.LeaderParty != party.Party ||
            raid.DefenderSide.LeaderParty != quest.QuestSettlement.Party ||
            ownerParty.CurrentSettlement != quest.QuestSettlement ||
            (ownerParty.MapEvent != null && ownerParty.MapEvent != raid) ||
            quest._defenderMobileParty != null)
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

        progress.Send(quest, ambush: true);
        return true;
    }

    public bool TryCleanupBattle(Quest quest)
    {
        if (!quest._deserterBattleFinalizedForTheFirstTime)
        {
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

    public bool ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (ModInformation.IsClient || Hero.MainHero.PartyBelongedTo == null) return false;
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

        return false;
    }

    public bool ApplySettlementLeft(Quest quest, MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsClient || Hero.MainHero.PartyBelongedTo == null) return false;
        var deserters = quest._deserterMobileParty;
        var raid = settlement.Party.MapEvent;
        if (party.IsMainParty && settlement == quest.QuestSettlement && deserters != null &&
            raid?.IsRaid == true && raid.AttackerSide.LeaderParty == deserters.Party)
        {
            quest.ApplyQuestResult(quest._questResultFail2);
            quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
        }

        return false;
    }

    public bool ApplyMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        var deserters = quest._deserterMobileParty;
        if (ModInformation.IsClient || deserters == null || !mapEvent.InvolvedParties.Contains(deserters.Party)) return false;
        if (!ownerParty.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return false;
        if (!mapEvent.InvolvedParties.Contains(owner)) return false;

        quest._deserterBattleFinalizedForTheFirstTime = true;
        if (mapEvent.WinningSide == owner.Side)
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

        return false;
    }

    public bool ApplyVillageBeingRaided(Quest quest, Village village)
    {
        if (ModInformation.IsClient || village.Settlement != quest.QuestSettlement) return false;
        var attacker = village.Settlement.Party.MapEvent?.AttackerSide.LeaderParty;
        if (attacker == null) return false;
        if (attacker == quest._deserterMobileParty?.Party)
        {
            if (Hero.MainHero.PartyBelongedTo?.CurrentSettlement == village.Settlement) return false;
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
            quest.CompleteQuestWithCancel(quest.OnQuestCancel2LogText);
        }

        return false;
    }

    public bool ApplyTick(Quest quest)
    {
        if (ModInformation.IsClient || !quest.IsOngoing || quest._currentState == State.DesertersAreDefeated) return false;
        var deserters = quest._deserterMobileParty;
        if (deserters == null || !deserters.IsActive)
        {
            quest.CompleteQuestWithCancel();
            return false;
        }
        if (deserters.MapEvent != null)
        {
            TryStartAmbush(quest);
            return false;
        }

        var owner = Hero.MainHero.PartyBelongedTo;
        if (owner == null && quest._currentState != State.DesertersDefeatedPlayer) return false;
        switch (quest._currentState)
        {
            case State.DesertersMovingToSettlement:
                if (owner.CurrentSettlement == quest.QuestSettlement) SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
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
            case State.DesertersDefeatedPlayer:
                SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
                break;
        }

        return false;
    }

    internal static void SetRaidBehaviorIfNeeded(MobileParty deserters, Settlement settlement)
    {
        if (deserters.DefaultBehavior == AiBehavior.RaidSettlement) return;
        SetPartyAiAction.GetActionForRaidingSettlement(deserters, settlement, MobileParty.NavigationType.Default, false, false);
    }
}
