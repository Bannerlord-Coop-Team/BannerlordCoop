#if DEBUG
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.MapEvents.NavalScenarios;

public enum NavalScenarioPhase
{
    None = 0,
    Staging = 1,
    Deploying = 2,
    Fighting = 3,
    Ended = 4,
}

public enum NavalScenarioStepStatus
{
    Pending = 0,
    Done = 1,
    Failed = 2,
}

/// <summary>DEBUG: one automatic action of a scenario on this machine, with its outcome once it ran.</summary>
public sealed class NavalScenarioStep
{
    public NavalScenarioStep(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public NavalScenarioStepStatus Status { get; set; }

    public string Detail { get; set; }
}

/// <summary>
/// [Game thread] DEBUG: the naval scenario this machine is running, its phase and its automatic steps. Held in memory only;
/// the next scenario replaces it.
/// </summary>
public interface INavalScenarioState
{
    /// <summary>The scenario, or null before the first one.</summary>
    NavalScenarioConfig Current { get; }

    NavalScenarioPhase Phase { get; }

    IReadOnlyList<NavalScenarioStep> Steps { get; }

    /// <summary>
    /// Makes <paramref name="config"/> current in the staging phase, with the steps this machine runs for it; a
    /// <paramref name="rejoining"/> client only re-enters the running battle.
    /// </summary>
    void Start(NavalScenarioConfig config, string localControllerId, bool isServer, bool rejoining = false);

    /// <summary>Moves the current scenario to <paramref name="phase"/>; phases only move forward.</summary>
    void Advance(NavalScenarioPhase phase);

    /// <summary>True while <paramref name="name"/> is a step of the current scenario that has not run yet.</summary>
    bool IsPending(string name);

    /// <summary>Records the outcome of step <paramref name="name"/>; ignored when it is not pending.</summary>
    void Complete(string name, bool succeeded, string detail);
}

/// <inheritdoc cref="INavalScenarioState"/>
public class NavalScenarioState : INavalScenarioState
{
    public const string AttackStep = "attack";
    public const string LateJoinStep = "late_join";
    public const string LateJoinAttackStep = "late_join_attack";
    public const string ReadyStep = "ready";
    public const string PlaceStep = "place";
    public const string RopeStep = "rope";
    public const string HelmStep = "helm";
    public const string SinkStep = "sink";
    public const string DisconnectStep = "disconnect";
    /// <summary>The player who disconnected presses Ready again in the battle it reconnected into.</summary>
    public const string RejoinStep = "rejoin";
    /// <summary>The player who reconnected enters the running battle again through the encounter menu's Attack.</summary>
    public const string RejoinAttackStep = "rejoin_attack";
    public const string RetreatStep = "retreat";
    /// <summary>The server waits for the campaign AI's engage to start the encounter preset's map event.</summary>
    public const string EngageStep = "engage";
    /// <summary>The server staged the blockade preset's siege, blockade and blockade battle.</summary>
    public const string BlockadeStep = "blockade";

    private List<NavalScenarioStep> steps = new List<NavalScenarioStep>();

    public NavalScenarioConfig Current { get; private set; }

    public NavalScenarioPhase Phase { get; private set; }

    public IReadOnlyList<NavalScenarioStep> Steps => steps;

    public void Start(NavalScenarioConfig config, string localControllerId, bool isServer, bool rejoining = false)
    {
        Current = config;
        Phase = config == null ? NavalScenarioPhase.None : NavalScenarioPhase.Staging;
        var names = rejoining ? RejoinStepsFor(config, localControllerId) : StepsFor(config, localControllerId, isServer);
        steps = names.Select(name => new NavalScenarioStep(name)).ToList();
        if (rejoining) Complete(DisconnectStep, true, "disconnected before this session; re-entering the running battle");
    }

    /// <summary>The steps of a player that reconnected while its scenario's battle runs: the disconnect, done, then re-entry.</summary>
    internal static IEnumerable<string> RejoinStepsFor(NavalScenarioConfig config, string localControllerId)
    {
        if (config == null || !config.DisconnectBattleHost || !config.ControllerIds.Contains(localControllerId)) yield break;

        yield return DisconnectStep;
        yield return RejoinAttackStep;
        yield return RejoinStep;
    }

    public void Advance(NavalScenarioPhase phase)
    {
        if (Current == null || phase <= Phase) return;
        Phase = phase;
    }

    public bool IsPending(string name) => Find(name)?.Status == NavalScenarioStepStatus.Pending;

    public void Complete(string name, bool succeeded, string detail)
    {
        var step = Find(name);
        if (step == null || step.Status != NavalScenarioStepStatus.Pending) return;

        step.Status = succeeded ? NavalScenarioStepStatus.Done : NavalScenarioStepStatus.Failed;
        step.Detail = detail;
    }

    private NavalScenarioStep Find(string name) => steps.FirstOrDefault(step => step.Name == name);

    /// <summary>The automatic steps one machine runs: the server joins a late joiner, each client acts for its own player.</summary>
    internal static IEnumerable<string> StepsFor(NavalScenarioConfig config, string localControllerId, bool isServer)
    {
        if (config == null) yield break;

        if (isServer)
        {
            if (config.LateJoinerControllerId != null) yield return LateJoinStep;
            if (config.CampaignEngage) yield return EngageStep;
            if (config.Blockade) yield return BlockadeStep;
            yield break;
        }

        // A client outside the scenario never enters its battle; it only reports the scenario.
        if (localControllerId == null || !config.ControllerIds.Contains(localControllerId)) yield break;

        if (localControllerId == config.InitiatorControllerId) yield return AttackStep;
        if (localControllerId == config.LateJoinerControllerId) yield return LateJoinAttackStep;
        yield return ReadyStep;

        if (config.Placement != NavalScenarioPlacement.Deployment) yield return PlaceStep;
        if (config.Placement == NavalScenarioPlacement.Boarding) yield return RopeStep;
        if (config.Placement == NavalScenarioPlacement.Ramming) yield return HelmStep;
        if (config.SinkingEnemyHull) yield return SinkStep;
        if (config.DisconnectBattleHost)
        {
            // Who disconnects depends on the elected host, so every client carries both steps and the others complete them.
            yield return DisconnectStep;
            yield return RejoinStep;
        }
        if (localControllerId == config.RetreatingControllerId) yield return RetreatStep;
    }
}
#endif
