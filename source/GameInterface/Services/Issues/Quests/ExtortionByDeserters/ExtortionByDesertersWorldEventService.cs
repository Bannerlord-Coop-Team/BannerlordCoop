using Common.Logging;
using System;
using System.Collections.Generic;
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
    void HourlyTick();
    void BattleStarted(PartyBase attackerParty);
    Dictionary<Quest, bool?> CaptureBattleOutcomes(MapEvent mapEvent);
    void BattleFinalized(Dictionary<Quest, bool?> outcomes);
    void MapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender);
    void SettlementLeft(MobileParty party, Settlement settlement);
    void VillageBeingRaided(Village village);
}

internal sealed class ExtortionByDesertersWorldEventService : IExtortionByDesertersWorldEventService
{
    private readonly IExtortionByDesertersProgress progress;
    private static readonly ILogger Logger = LogManager.GetLogger<ExtortionByDesertersWorldEventService>();
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IOutcomeDispatcher dispatcher;

    public ExtortionByDesertersWorldEventService(IExtortionByDesertersProgress progress,
        IIssueOwnerResolver ownerResolver, IOutcomeDispatcher dispatcher)
    {
        this.progress = progress;
        this.ownerResolver = ownerResolver;
        this.dispatcher = dispatcher;
    }

    public void HourlyTick() => RunWithOwnerParty(ApplyTick);

    public void BattleStarted(PartyBase attackerParty) => RunWithOwnerParty((quest, owner) =>
    {
        if (attackerParty == quest._deserterMobileParty?.Party) StartAmbush(quest, owner);
    });

    public void MapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender) =>
        RunWithOwnerParty((quest, owner) => ApplyMapEventStarted(quest, owner, mapEvent, attacker, defender));

    public void SettlementLeft(MobileParty party, Settlement settlement) =>
        RunWithOwnerParty((quest, owner) => ApplySettlementLeft(quest, owner, party, settlement));

    public void VillageBeingRaided(Village village) =>
        RunWithOwnerParty((quest, owner) => ApplyVillageBeingRaided(quest, owner, village));

    private void RunWithOwnerParty(Action<Quest, MobileParty> apply) => dispatcher.RunAsOwners<Quest>(quest =>
    {
        // Keep the registered party even when captivity detaches the owner hero from it.
        ownerResolver.TryResolveOwnerParty(IssueManager.GetIssueOfQuest(quest), out var owner);
        apply(quest, owner);
    });

    private void StartAmbush(Quest quest, MobileParty owner)
    {
        var party = quest._deserterMobileParty;
        if (party == null || owner == null) return;
        // The defenders exist once the ambush has started.
        if (quest._defenderMobileParty != null) return;

        var raid = party.MapEvent;
        var isQuestRaid = raid?.IsRaid == true && !raid.HasWinner && !raid.IsFinalized &&
            raid.AttackerSide.LeaderParty == party.Party &&
            raid.DefenderSide.LeaderParty == quest.QuestSettlement.Party;
        var ownerCanDefend = owner.CurrentSettlement == quest.QuestSettlement &&
            (owner.MapEvent == null || owner.MapEvent == raid);
        if (!isQuestRaid || !ownerCanDefend)
        {
            return;
        }

        // The server half of vanilla StartAmbushEncounter. The owner opens the encounter when the Ambush notice arrives.
        quest.CreateDefenderParty();
        quest._defenderMobileParty.MapEventSide = raid.DefenderSide;
        owner.MapEventSide = raid.DefenderSide;
        party.IgnoreByOtherPartiesTill(CampaignTime.Now - CampaignTime.Hours(1f));
        if (quest.QuestSettlement.MilitiaPartyComponent != null)
        {
            quest.QuestSettlement.MilitiaPartyComponent.MobileParty.MapEventSide = null;
        }

        progress.Send(quest, notice: ExtortionByDesertersNotice.Ambush);
    }

    public Dictionary<Quest, bool?> CaptureBattleOutcomes(MapEvent mapEvent)
    {
        var outcomes = new Dictionary<Quest, bool?>();
        RunWithOwnerParty((quest, owner) =>
        {
            var deserters = quest._deserterMobileParty;
            if (deserters == null || deserters.MapEvent != mapEvent ||
                !mapEvent.InvolvedParties.Contains(deserters.Party)) return;

            bool? ownerWon = null;
            if (owner != null && mapEvent.InvolvedParties.Contains(owner.Party))
                ownerWon = mapEvent.WinningSide == owner.Party.Side;
            outcomes.Add(quest, ownerWon);
        });
        return outcomes;
    }

    public void BattleFinalized(Dictionary<Quest, bool?> outcomes) => dispatcher.RunAsOwners<Quest>(quest =>
    {
        if (outcomes.TryGetValue(quest, out var ownerWon)) ApplyBattleFinalized(quest, ownerWon);
    });

    internal void ApplyBattleFinalized(Quest quest, bool? ownerWon)
    {
        if (!ownerWon.HasValue)
        {
            // A battle without the owner can still destroy the deserters.
            CancelIfDesertersGone(quest);
            return;
        }

        quest.DestroyDefenderParty();
        quest._defenderMobileParty = null;
        if (ownerWon.Value)
        {
            quest._currentState = State.DesertersAreDefeated;
            quest.DestroyDeserterParty();
            quest._deserterMobileParty = null;
            // AddLog forwards the journal and the cleared party bindings in one snapshot.
            quest.AddLog(quest.OnDeserterPartyDefeatedLogText);
            return;
        }

        SetRaidBehaviorIfNeeded(quest._deserterMobileParty, quest.QuestSettlement);
        quest._currentState = State.DesertersDefeatedPlayer;
        progress.Send(quest);
    }

    private void ApplyMapEventStarted(Quest quest, MobileParty owner, MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (owner == null) return;
        var attacksVillageParty = mapEvent.IsFieldBattle && defender.IsMobile &&
            defender.MobileParty.HomeSettlement == quest.QuestSettlement &&
            defender != quest._deserterMobileParty?.Party;
        if (attacker != owner.Party || !attacksVillageParty) return;

        quest.CompleteQuestWithFail(quest.OnQuestFailed3LogText);
        quest.ApplyQuestResult(quest._questResultFail3);
    }

    private void ApplySettlementLeft(Quest quest, MobileParty owner, MobileParty party, Settlement settlement)
    {
        if (owner == null || party != owner || settlement != quest.QuestSettlement) return;

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
            ApplyTick(quest, owner);
        }
    }

    private void ApplyVillageBeingRaided(Quest quest, MobileParty owner, Village village)
    {
        if (owner == null || village.Settlement != quest.QuestSettlement) return;
        var attacker = village.Settlement.Party.MapEvent.AttackerSide.LeaderParty;
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

    internal void ApplyTick(Quest quest, MobileParty owner)
    {
        if (!quest.IsOngoing || quest._currentState == State.DesertersAreDefeated) return;
        if (CancelIfDesertersGone(quest)) return;
        var deserters = quest._deserterMobileParty;
        // Vanilla returns while a map event is active. Co-op starts the owner ambush after the server creates its defender.
        if (deserters.MapEvent != null)
        {
            StartAmbush(quest, owner);
            return;
        }

        if (quest._currentState == State.DesertersDefeatedPlayer)
        {
            SetRaidBehaviorIfNeeded(deserters, quest.QuestSettlement);
            return;
        }

        if (owner == null) return;
        // RunAsOwners selects the hero. A captured owner no longer belongs to the registered party.
        var ownerIsCaptive = Hero.MainHero.PartyBelongedTo != owner;
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

    private static void SetRaidBehaviorIfNeeded(MobileParty deserters, Settlement settlement)
    {
        if (deserters.DefaultBehavior == AiBehavior.RaidSettlement) return;
        SetPartyAiAction.GetActionForRaidingSettlement(deserters, settlement, MobileParty.NavigationType.Default,
            isFromPort: false, isTargetingPort: false);
    }
}
