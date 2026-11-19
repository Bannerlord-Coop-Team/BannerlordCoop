using Common;
using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using Helpers;
using Serilog;
using System.Linq;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal interface IExtortionByDesertersWorldEventService
{
    bool TryStartAmbush(Quest quest, PartyBase attackerParty = null);
    void ApplyBattleFinalized(Quest quest);
    void ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender);
    void ApplySettlementLeft(Quest quest, MobileParty party, Settlement settlement);
    void ApplyMapEventEnded(Quest quest, MapEvent mapEvent);
    void ApplyVillageBeingRaided(Quest quest, Village village);
    void ApplyTick(Quest quest);
}

internal sealed class ExtortionByDesertersWorldEventService : IExtortionByDesertersWorldEventService
{
    private readonly IExtortionByDesertersProgress progress;
    private static readonly ILogger Logger = LogManager.GetLogger<ExtortionByDesertersWorldEventService>();
    private readonly IIssueOwnerParty ownerPartyResolver;

    public ExtortionByDesertersWorldEventService(IExtortionByDesertersProgress progress, IIssueOwnerParty ownerParty)
    {
        this.progress = progress;
        ownerPartyResolver = ownerParty;
    }

    public bool TryStartAmbush(Quest quest, PartyBase attackerParty = null)
    {
        var party = quest._deserterMobileParty;
        var raid = party?.MapEvent;
        if (attackerParty != null && attackerParty != party?.Party) return false;
        if (!ownerPartyResolver.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner))
        {
            return false;
        }
        var isQuestRaid = raid?.IsRaid == true && !raid.HasWinner && !raid.IsFinalized &&
            raid.AttackerSide.LeaderParty == party?.Party &&
            raid.DefenderSide.LeaderParty == quest.QuestSettlement.Party;
        var ownerCanDefend = owner.CurrentSettlement == quest.QuestSettlement &&
            (owner.MapEvent == null || owner.MapEvent == raid);
        if (!isQuestRaid || !ownerCanDefend || quest._defenderMobileParty != null)
        {
            return false;
        }

        quest.CreateDefenderParty();
        quest._defenderMobileParty.MapEventSide = raid.DefenderSide;
        owner.MapEventSide = raid.DefenderSide;
        party.IgnoreByOtherPartiesTill(CampaignTime.Now - CampaignTime.Hours(1f));
        if (quest.QuestSettlement.MilitiaPartyComponent != null)
        {
            quest.QuestSettlement.MilitiaPartyComponent.MobileParty.MapEventSide = null;
        }

        progress.Send(quest, notice: ExtortionByDesertersNotice.Ambush);
        return true;
    }

    public void ApplyBattleFinalized(Quest quest)
    {
        if (!quest._deserterBattleFinalizedForTheFirstTime)
        {
            if (quest.IsOngoing && quest._deserterMobileParty?.IsActive == false)
            {
                quest.CompleteQuestWithCancel();
            }
            return;
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
    }

    public void ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (ModInformation.IsClient) return;
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
        if (ModInformation.IsClient) return;
        if (!ownerPartyResolver.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return;
        var deserters = quest._deserterMobileParty;
        var raid = settlement.Party.MapEvent;
        var desertersRaidQuestVillage = settlement == quest.QuestSettlement && deserters != null &&
            raid?.IsRaid == true && raid.AttackerSide.LeaderParty == deserters.Party;
        var ownerLeftQuestVillage = party == owner && desertersRaidQuestVillage;
        if (ownerLeftQuestVillage)
        {
            quest.ApplyQuestResult(quest._questResultFail2);
            quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
        }

        if (quest.IsOngoing && party == owner && settlement == quest.QuestSettlement)
        {
            ApplyTick(quest);
        }
    }

    public void ApplyMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        var deserters = quest._deserterMobileParty;
        if (ModInformation.IsClient || deserters == null || !mapEvent.InvolvedParties.Contains(deserters.Party)) return;
        if (!ownerPartyResolver.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner))
        {
            Logger.Warning("Could not resolve the owner party for extortion quest {quest}", quest.StringId);
            return;
        }
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
        var attacker = village.Settlement.Party.MapEvent.AttackerSide.LeaderParty;
        if (!ownerPartyResolver.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return;
        if (attacker == quest._deserterMobileParty?.Party)
        {
            // Vanilla checks PlayerEncounter. The co-op owner party is the authoritative substitute.
            if (owner.CurrentSettlement == village.Settlement) return;
            quest.ApplyQuestResult(quest._questResultFail2);
            quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
        }
        else if (attacker == owner.Party)
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
        // Vanilla has no active-party cancellation here. Co-op must release an owner quest after its party disappears.
        if (deserters == null || !deserters.IsActive)
        {
            quest.CompleteQuestWithCancel();
            return;
        }
        // Vanilla returns while a map event is active. Co-op starts the owner ambush after the server creates its defender.
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

        if (!ownerPartyResolver.TryGetOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return;
        switch (quest._currentState)
        {
            case State.DesertersMovingToSettlement:
                if (owner.CurrentSettlement == quest.QuestSettlement)
                {
                    SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
                }
                else if (deserters.Position.Distance(owner.Position) <= deserters.SeeingRange * Quest.SightRatioToStartEvadingPlayerParty)
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
                if (deserters.Position.Distance(owner.Position) > owner.SeeingRange + Quest.SightOffsetToLoseDeserterParty)
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
