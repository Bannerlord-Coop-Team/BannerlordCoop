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

    /// <summary>Makes <paramref name="config"/> current in the staging phase, with the steps this machine runs for it.</summary>
    void Start(NavalScenarioConfig config, string localControllerId, bool isServer);

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
    public const string RetreatStep = "retreat";

    private List<NavalScenarioStep> steps = new List<NavalScenarioStep>();

    public NavalScenarioConfig Current { get; private set; }

    public NavalScenarioPhase Phase { get; private set; }

    public IReadOnlyList<NavalScenarioStep> Steps => steps;

    public void Start(NavalScenarioConfig config, string localControllerId, bool isServer)
    {
        Current = config;
        Phase = config == null ? NavalScenarioPhase.None : NavalScenarioPhase.Staging;
        steps = StepsFor(config, localControllerId, isServer).Select(name => new NavalScenarioStep(name)).ToList();
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
        if (config.DisconnectBattleHost) yield return DisconnectStep;
        if (localControllerId == config.RetreatingControllerId) yield return RetreatStep;
    }
}
#endif
