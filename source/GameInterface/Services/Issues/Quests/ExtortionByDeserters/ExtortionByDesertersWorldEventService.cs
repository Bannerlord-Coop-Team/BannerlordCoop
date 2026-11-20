using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using Serilog;
using System.Linq;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal interface IExtortionByDesertersWorldEventService
{
    // With no attacker, the hourly tick retries a raid already in progress.
    bool TryStartAmbush(Quest quest, PartyBase attackerParty = null);
    bool IsDeserterBattle(Quest quest, MapEvent mapEvent);
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
    private readonly IIssueOwnerResolver ownerResolver;

    public ExtortionByDesertersWorldEventService(IExtortionByDesertersProgress progress,
        IIssueOwnerResolver ownerResolver)
    {
        this.progress = progress;
        this.ownerResolver = ownerResolver;
    }

    public bool TryStartAmbush(Quest quest, PartyBase attackerParty = null)
    {
        var party = quest._deserterMobileParty;
        if (attackerParty != null && (party == null || attackerParty != party.Party)) return false;
        if (!ownerResolver.TryResolveOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner))
        {
            return false;
        }
        if (party == null) return false;

        var raid = party.MapEvent;
        var isQuestRaid = raid?.IsRaid == true && !raid.HasWinner && !raid.IsFinalized &&
            raid.AttackerSide.LeaderParty == party.Party &&
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

    public bool IsDeserterBattle(Quest quest, MapEvent mapEvent)
    {
        var deserters = quest._deserterMobileParty;
        return deserters != null && deserters.MapEvent == mapEvent && mapEvent.InvolvedParties.Contains(deserters.Party);
    }

    public void ApplyBattleFinalized(Quest quest)
    {
        if (!quest._deserterBattleFinalizedForTheFirstTime)
        {
            // A battle without the owner leaves the flag unset, but can still destroy the deserters.
            CancelIfDesertersGone(quest);
            return;
        }

        quest._deserterBattleFinalizedForTheFirstTime = false;
        var desertersDefeated = quest._currentState == State.DesertersAreDefeated;
        if (desertersDefeated)
        {
            quest.DestroyDeserterParty();
            quest._deserterMobileParty = null;
        }

        quest.DestroyDefenderParty();
        quest._defenderMobileParty = null;
        if (desertersDefeated)
        {
            // AddLog forwards the journal and the cleared party bindings in one snapshot.
            quest.AddLog(quest.OnDeserterPartyDefeatedLogText);
        }
        else
        {
            progress.Send(quest);
        }
    }

    public void ApplyMapEventStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (!ownerResolver.TryResolveOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return;
        var attacksVillageParty = mapEvent.IsFieldBattle && defender.IsMobile &&
            defender.MobileParty.HomeSettlement == quest.QuestSettlement &&
            defender != quest._deserterMobileParty?.Party;
        if (attacker != owner.Party || !attacksVillageParty) return;

        quest.CompleteQuestWithFail(quest.OnQuestFailed3LogText);
        quest.ApplyQuestResult(quest._questResultFail3);
    }

    public void ApplySettlementLeft(Quest quest, MobileParty party, Settlement settlement)
    {
        if (!ownerResolver.TryResolveOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner)) return;
        if (party != owner || settlement != quest.QuestSettlement) return;

        var deserters = quest._deserterMobileParty;
        var raid = settlement.Party.MapEvent;
        var desertersRaidQuestVillage = deserters != null &&
            raid?.IsRaid == true && raid.AttackerSide.LeaderParty == deserters.Party;
        if (desertersRaidQuestVillage)
        {
            quest.ApplyQuestResult(quest._questResultFail2);
            quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
        }

        if (quest.IsOngoing)
        {
            // This replaces the vanilla leave-menu tick in GameMenuOptionSelected.
            ApplyTick(quest);
        }
    }

    public void ApplyMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        if (!IsDeserterBattle(quest, mapEvent)) return;
        if (!ownerResolver.TryResolveOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner))
        {
            Logger.Warning("Could not resolve the owner party for extortion quest {quest}", quest.StringId);
            return;
        }
        if (!mapEvent.InvolvedParties.Contains(owner.Party)) return;

        quest._deserterBattleFinalizedForTheFirstTime = true;
        if (mapEvent.WinningSide == owner.Party.Side)
        {
            quest._currentState = State.DesertersAreDefeated;
        }
        else
        {
            SetRaidBehaviorIfNeeded(quest._deserterMobileParty, quest.QuestSettlement);
            // Finalization publishes the loss after the defenders are cleared.
            quest._currentState = State.DesertersDefeatedPlayer;
        }
    }

    public void ApplyVillageBeingRaided(Quest quest, Village village)
    {
        if (village.Settlement != quest.QuestSettlement) return;
        var attacker = village.Settlement.Party.MapEvent.AttackerSide.LeaderParty;
        if (!ownerResolver.TryResolveOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner))
        {
            Logger.Warning("Could not resolve the owner party for extortion quest {quest}", quest.StringId);
            return;
        }
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

    private static bool CancelIfDesertersGone(Quest quest)
    {
        // A defeated quest has already cleared its parties and is waiting for hand-in.
        if (!quest.IsOngoing || quest._currentState == State.DesertersAreDefeated ||
            quest._deserterMobileParty?.IsActive == true) return false;

        Logger.Warning("Cancelling extortion quest {quest}: the deserter party is gone", quest.StringId);
        quest.CompleteQuestWithCancel();
        return true;
    }

    public void ApplyTick(Quest quest)
    {
        if (!quest.IsOngoing || quest._currentState == State.DesertersAreDefeated) return;
        if (CancelIfDesertersGone(quest)) return;
        var deserters = quest._deserterMobileParty;
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

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (!ownerResolver.TryResolveOwnerParty(issue, out var owner) ||
            !ownerResolver.TryResolveOwnerHero(issue, out var ownerHero)) return;

        var ownerIsCaptive = ownerHero.PartyBelongedTo != owner;
        if (ownerIsCaptive) return;

        switch (quest._currentState)
        {
            case State.DesertersMovingToSettlement:
                TickMovingToSettlement(quest, deserters, owner);
                break;
            case State.DesertersRunningAwayFromPlayer:
                TickRunningAway(quest, deserters, owner);
                break;
        }
    }

    private void TickMovingToSettlement(Quest quest, MobileParty deserters, MobileParty owner)
    {
        if (owner.CurrentSettlement == quest.QuestSettlement)
        {
            SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
            return;
        }

        var desertersSeeOwner = deserters.Position.Distance(owner.Position) <=
            deserters.SeeingRange * Quest.SightRatioToStartEvadingPlayerParty;
        if (desertersSeeOwner)
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
    }

    private static void TickRunningAway(Quest quest, MobileParty deserters, MobileParty owner)
    {
        var ownerLostDeserters = deserters.Position.Distance(owner.Position) >
            owner.SeeingRange + Quest.SightOffsetToLoseDeserterParty;
        if (ownerLostDeserters)
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
        else
        {
            quest.HandleDesertersRunningAway();
        }
    }

    internal static void SetRaidBehaviorIfNeeded(MobileParty deserters, Settlement settlement)
    {
        if (deserters.DefaultBehavior == AiBehavior.RaidSettlement) return;
        SetPartyAiAction.GetActionForRaidingSettlement(deserters, settlement, MobileParty.NavigationType.Default, false, false);
    }
}
