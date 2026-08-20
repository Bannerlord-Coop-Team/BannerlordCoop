using Common;
using GameInterface.Services.MobileParties.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents;

public enum AiSiegeTerminalDecision
{
    None,
    Defer,
    Assault,
    Withdraw,
}

public readonly struct AiSiegeTerminalContext
{
    public bool IsFoodProblem { get; }
    public bool IsPrepared { get; }
    public bool IsPlayerLed { get; }
    public bool IsCurrentSiege { get; }
    public bool HasActiveTransition { get; }
    public bool IsAssaultViable { get; }

    public AiSiegeTerminalContext(
        bool isFoodProblem,
        bool isPrepared,
        bool isPlayerLed,
        bool isCurrentSiege,
        bool hasActiveTransition,
        bool isAssaultViable)
    {
        IsFoodProblem = isFoodProblem;
        IsPrepared = isPrepared;
        IsPlayerLed = isPlayerLed;
        IsCurrentSiege = isCurrentSiege;
        HasActiveTransition = hasActiveTransition;
        IsAssaultViable = isAssaultViable;
    }
}

public readonly struct AiSiegeTerminalTransitionState
{
    public MobileParty LeaderParty { get; }
    public SiegeEvent SiegeEvent { get; }

    public bool IsValid => LeaderParty != null && SiegeEvent != null;

    public AiSiegeTerminalTransitionState(MobileParty leaderParty, SiegeEvent siegeEvent)
    {
        LeaderParty = leaderParty;
        SiegeEvent = siegeEvent;
    }
}

public interface IAiSiegeTerminalPolicy
{
    AiSiegeTerminalDecision GetDecision(AiSiegeTerminalContext context);
    AiSiegeTerminalDecision ResolveFoodProblem(AiSiegeTerminalTransitionState state);
    void RetryDeferredTransitions();
    void SyncData(IDataStore dataStore);
}

/// <summary>Resolves an AI siege after vanilla disperses its starving army.</summary>
internal class AiSiegeTerminalPolicy : IAiSiegeTerminalPolicy
{
    private const string DeferredLeaderSaveKey = "_coop_ai_siege_terminal_leaders";
    private const string DeferredSiegeEventSaveKey = "_coop_ai_siege_terminal_events";

    private readonly IAiSiegeAssaultReadiness readiness;
    private readonly ILogger logger;
    private readonly List<AiSiegeTerminalTransitionState> deferredTransitions = new();

    public AiSiegeTerminalPolicy(
        IAiSiegeAssaultReadiness readiness,
        ILogger logger)
    {
        this.readiness = readiness;
        this.logger = logger;
    }

    public AiSiegeTerminalDecision GetDecision(AiSiegeTerminalContext context)
    {
        // World state is the idempotency marker, so a save cannot retain a stale pending transition.
        if (!context.IsFoodProblem
            || context.IsPlayerLed
            || !context.IsCurrentSiege)
        {
            return AiSiegeTerminalDecision.None;
        }

        if (context.HasActiveTransition) return AiSiegeTerminalDecision.Defer;

        return context.IsPrepared && context.IsAssaultViable
            ? AiSiegeTerminalDecision.Assault
            : AiSiegeTerminalDecision.Withdraw;
    }

    public AiSiegeTerminalDecision ResolveFoodProblem(AiSiegeTerminalTransitionState state)
    {
        if (!state.IsValid) return AiSiegeTerminalDecision.None;

        var leader = state.LeaderParty;
        var siegeEvent = state.SiegeEvent;
        var camp = siegeEvent.BesiegerCamp;
        var settlement = siegeEvent.BesiegedSettlement;
        bool isCurrentSiege = settlement?.SiegeEvent == siegeEvent
            && camp?.LeaderParty == leader
            && leader.BesiegerCamp == camp;
        bool hasActiveTransition = leader.MapEvent != null || settlement?.Party.MapEvent != null;
        var readinessResult = isCurrentSiege
            ? readiness.Evaluate(camp)
            : default;
        var context = new AiSiegeTerminalContext(
            isFoodProblem: true,
            isPrepared: camp?.IsPreparationComplete == true,
            isPlayerLed: leader.IsPlayerParty(),
            isCurrentSiege,
            hasActiveTransition,
            readinessResult.IsViable);
        var decision = GetDecision(context);

        if (decision == AiSiegeTerminalDecision.Defer)
        {
            Defer(state);
        }
        else if (decision == AiSiegeTerminalDecision.Assault)
        {
            logger.Information(
                "Starving AI siege at {SettlementId} is starting its viable assault: attacker={AttackerStrength:0.00} defender={DefenderStrength:0.00} ratio={PowerRatio:0.000} chance={AssaultChance:0.000}",
                settlement.StringId,
                readinessResult.AttackerStrength,
                readinessResult.DefenderStrength,
                readinessResult.PowerRatioBeforeEquipment,
                readinessResult.AssaultChance);
            StartBattleAction.ApplyStartAssaultAgainstWalls(leader, settlement);
        }
        else if (decision == AiSiegeTerminalDecision.Withdraw)
        {
            string reason = camp.IsPreparationComplete
                ? "an assault is not viable"
                : "preparations are incomplete";
            logger.Information(
                "Starving AI siege at {SettlementId} is withdrawing because {Reason}: attacker={AttackerStrength:0.00} defender={DefenderStrength:0.00} ratio={PowerRatio:0.000} chance={AssaultChance:0.000}",
                settlement.StringId,
                reason,
                readinessResult.AttackerStrength,
                readinessResult.DefenderStrength,
                readinessResult.PowerRatioBeforeEquipment,
                readinessResult.AssaultChance);
            camp.RemoveAllSiegeParties();
        }

        return decision;
    }

    internal void Defer(AiSiegeTerminalTransitionState state)
    {
        foreach (var pending in deferredTransitions)
        {
            if (pending.LeaderParty == state.LeaderParty && pending.SiegeEvent == state.SiegeEvent)
                return;
        }

        deferredTransitions.Add(state);
    }

    public void SyncData(IDataStore dataStore)
    {
        List<MobileParty> leaders = null;
        List<SiegeEvent> siegeEvents = null;
        if (dataStore.IsSaving)
        {
            leaders = new List<MobileParty>();
            siegeEvents = new List<SiegeEvent>();
            if (ModInformation.IsServer)
            {
                foreach (var transition in deferredTransitions)
                {
                    leaders.Add(transition.LeaderParty);
                    siegeEvents.Add(transition.SiegeEvent);
                }
            }
        }

        dataStore.SyncData(DeferredLeaderSaveKey, ref leaders);
        dataStore.SyncData(DeferredSiegeEventSaveKey, ref siegeEvents);
        if (!dataStore.IsLoading) return;

        deferredTransitions.Clear();
        if (ModInformation.IsClient || leaders == null || siegeEvents == null) return;

        int count = Math.Min(leaders.Count, siegeEvents.Count);
        for (int i = 0; i < count; i++)
        {
            var transition = new AiSiegeTerminalTransitionState(leaders[i], siegeEvents[i]);
            if (transition.IsValid)
                Defer(transition);
        }
    }

    public void RetryDeferredTransitions()
    {
        RetryDeferredTransitions(state => { ResolveFoodProblem(state); });
    }

    internal void RetryDeferredTransitions(Action<AiSiegeTerminalTransitionState> resolve)
    {
        if (deferredTransitions.Count == 0) return;

        var pending = deferredTransitions.ToArray();
        deferredTransitions.Clear();
        foreach (var state in pending)
        {
            resolve(state);
        }
    }
}
