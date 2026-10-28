using Common.Logging;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Drives the coop naval services of one battle mission and tears them down with it.</summary>
public class CoopNavalBattleBehavior : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalBattleBehavior>();

    // Fewer agents than this leaving in one tick is ordinary combat; more is worth a line in the log.
    internal const int AgentDropThreshold = 10;

    private readonly INavalSteeredCopies steeredCopies;
    private readonly NavalShipsLogic shipsLogic;
    private readonly INavalTickFailureLog fixedTickFailures;
    private readonly INavalTickFailureLog missionTickFailures;
    private readonly INavalLateHullDeployment lateHullDeployment;
    private readonly INavalBallistaTargets ballistaTargets;
    private int lastAgentCount = -1;
    private bool retreated;

    // Takes one failure log per tick kind, so a flood on one of them can't hide the first lines of the other.
    public CoopNavalBattleBehavior(INavalBattleServices services, INavalSteeredCopies steeredCopies, INavalNpcFleetSpawner npcFleetSpawner,
        NavalShipsLogic shipsLogic, INavalTickFailureLog fixedTickFailures, INavalTickFailureLog missionTickFailures,
        INavalLateHullDeployment lateHullDeployment, INavalBallistaTargets ballistaTargets)
    {
        Services = services;
        this.steeredCopies = steeredCopies;
        NpcFleetSpawner = npcFleetSpawner;
        this.shipsLogic = shipsLogic;
        this.fixedTickFailures = fixedTickFailures;
        this.missionTickFailures = missionTickFailures;
        this.lateHullDeployment = lateHullDeployment;
        this.ballistaTargets = ballistaTargets;
        shipsLogic.ShipSpawnedEvent += OnShipSpawned;
        // Fires in NavalShipsLogic.OnEndMission before it removes every hull, so the ropes are still attached. It runs before
        // the battle controller leaves (NavalShipsLogic is an earlier behavior), so the last fates precede MissionLeft.
        shipsLogic.MissionEndEvent += OnMissionEnding;
        // Also fires for every hull as NavalShipsLogic.OnEndMission removes them, after the final ropes went out.
        shipsLogic.BeforeShipRemovedEvent += OnBeforeShipRemoved;
        shipsLogic.ShipRemovedEvent += OnShipRemoved;
        CoopNavalMissionScope.IsActive = true;
    }

    /// <summary>The battle's naval services, which the debug commands read.</summary>
    public INavalBattleServices Services { get; }

    /// <summary>Fields the AI fleet on the battle host; kept apart from the services because it binds NavalDLC types.</summary>
    public INavalNpcFleetSpawner NpcFleetSpawner { get; }

    // Ticks the naval side of the battle. A throw is logged throttled, so the later behaviours and vanilla's naval logic still
    // tick this frame instead of the whole mission stalling on one broken step.
    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        try
        {
            TickNaval(dt);
        }
        catch (Exception exception)
        {
            missionTickFailures.Report(exception, "Ticking the naval services", Mission);
        }

        LogAgentCountDrop();
#if DEBUG
        NavalImmortalCoopCommand.ApplyArmed();
#endif
    }

    // Snaps the copies, then ticks the services, the AI fleet and the tactic refresh, in that order.
    private void TickNaval(float dt)
    {
        // A snap teleports the hull and the agents on it, so it runs here rather than inside the fixed physics step.
        steeredCopies.ApplySnaps();
        Services.Tick(dt);
        NpcFleetSpawner.Tick();
        // After the services and the fleet, whose arrivals and fielding spawn the hulls this checks.
        lateHullDeployment.Apply(shipsLogic.AllShips);
    }

    // Runs inside FinishMissionLoading, so the battle's water time is in place before the loading screen lifts.
    public override void AfterStart()
    {
        base.AfterStart();
        Services.WaterClock.Start();
    }

    // Copies are steered with the physics step, so a velocity change lands on the body before it integrates. The engine calls
    // the fixed tick with no managed caller, so a throw here would crash the client; it is logged instead.
    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        try
        {
            steeredCopies.Tick(fixedDt);
        }
        catch (Exception exception)
        {
            fixedTickFailures.Report(exception, "Steering the copies on the fixed tick", Mission);
        }
    }

    // Logs the hull set the battle starts with. Vanilla runs this after every OnDeploymentFinished, so the controllers are the
    // ones DefaultNavalMissionLogic switched the hulls to when deployment ended.
    public override void OnAfterDeploymentFinished()
    {
        base.OnAfterDeploymentFinished();
        var hulls = shipsLogic.AllShips.Select(ship =>
            $"{ship.Index}:{ship.Team?.TeamSide}/{ship.Formation?.FormationIndex}" +
            $"{(NavalForeignHulls.Contains(ship) ? ":copy" : "")}:{ship.Controller?.ControllerType}" +
            $"{(ship.IsRemoved || !ship.GameEntity.IsValid ? ":removed" : "")}");
        Logger.Information("[NavalSync] Deployment finished with {Count} hull(s) [{Hulls}], {Copies} copy(ies), {Agents} agent(s)",
            shipsLogic.AllShips.Count, string.Join(", ", hulls), NavalForeignHulls.All.Count(), Mission.Agents.Count);
    }

    // Vanilla's Tab and Retreat runs OnRetreatMission on every logic before it ends the mission and removes the hulls.
    public override void OnRetreatMission()
    {
        base.OnRetreatMission();
        retreated = true;
    }

    // Fires inside SpawnShip, before its caller sets the hull up further. Vanilla's deployment pass only reaches the hulls that
    // exist when deployment ends, so a later hull (AI fleet, copy, hull swap) is deployed here.
    private void OnShipSpawned(MissionShip ship)
    {
        if (Mission.IsDeploymentFinished) lateHullDeployment.Deploy(ship);
    }

    // Hands a hull vanilla is about to remove to the services, with the reason its fate is reported under.
    private void OnBeforeShipRemoved(MissionShip ship)
    {
        bool retreatOrdered = ship.IsShipOrderActive && ship.ShipOrder.MovementOrderEnum == ShipOrder.ShipMovementOrderEnum.Retreat;
        var reason = retreated || retreatOrdered ? ShipRemovalReason.Retreat : ShipRemovalReason.Leave;
        Services.OnBeforeHullRemoved(ship, reason, shipsLogic.IsMissionEnding);
    }

    // Vanilla frees a removed hull's entity right after this event, or pools it during deployment, so no copy reference, rope
    // record, rope AI preference or ballista target may outlive it, whichever path removed it (departure, replace, retreat).
    private void OnShipRemoved(MissionShip ship)
    {
        bool wasCopy = NavalForeignHulls.Contains(ship);
        steeredCopies.Forget(ship);
        NavalRopeRemoval.ForgetRemovedHull(shipsLogic.AllShips, ship);
        ballistaTargets.ForgetHull(shipsLogic.AllShips, ship);
        if (shipsLogic.IsMissionEnding) return;

        Logger.Information("[NavalSync] Hull {Index} on {Team} formation {Formation} removed (copy {Copy}, pooled {Pooled})",
            ship.Index, ship.Team?.TeamSide, ship.Formation?.FormationIndex, wasCopy, shipsLogic.IsDeploymentMode);
    }

    // Whether the agent count fell by more in one tick than ordinary combat explains.
    internal static bool IsAgentCountDrop(int before, int after) => before >= 0 && before - after >= AgentDropThreshold;

    // Logs a large one-tick drop in the mission's agent count.
    private void LogAgentCountDrop()
    {
        int count = Mission.Agents.Count;
        if (IsAgentCountDrop(lastAgentCount, count))
            Logger.Warning("[NavalSync] Active agents fell from {Before} to {After} in one tick (deployment mode {DeploymentMode})",
                lastAgentCount, count, shipsLogic.IsDeploymentMode);
        lastAgentCount = count;
    }

    // Vanilla's mission end event, called from its hull removal: a throw is logged so vanilla still removes the hulls.
    private void OnMissionEnding()
    {
        try
        {
            Services.OnMissionEnding();
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[NavalSync] The naval services threw at the mission end; the last ropes or fates may be missing");
        }
    }

    // Disposes the services and resets the naval statics. A throwing service is logged, so the resets, the later behaviours
    // and vanilla's own mission end still run.
    public override void OnEndMissionInternal()
    {
        CoopNavalMissionScope.IsActive = false;
        shipsLogic.MissionEndEvent -= OnMissionEnding;
        shipsLogic.BeforeShipRemovedEvent -= OnBeforeShipRemoved;
        shipsLogic.ShipRemovedEvent -= OnShipRemoved;
        shipsLogic.ShipSpawnedEvent -= OnShipSpawned;
        try
        {
            Services.Dispose();
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[NavalSync] Disposing the naval services threw; the naval statics are reset anyway");
        }

        NpcFleetSpawner.Dispose();

        NavalRopes.Clear();
#if DEBUG
        NavalHelmOverride.Clear();
        NavalContactProbe.Clear();
        NavalImmortalCoopCommand.ClearArmed();
#endif
        // The static mirror's reset for a copy the engine never forgot; the engine's own entries die with the mission.
        NavalForeignHulls.Clear();
        NavalShipDamageGate.Clear();
        NavalPlayerDeploymentSlot.Reset();
        base.OnEndMissionInternal();
    }
}
