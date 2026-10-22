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

    private readonly INavalBattleServices services;
    private readonly NavalShipsLogic shipsLogic;
    private readonly Action tickNpcFleet;
    private int lastAgentCount = -1;
    private bool retreated;

    public CoopNavalBattleBehavior(INavalBattleServices services, INavalNpcFleetSpawner npcFleetSpawner, NavalShipsLogic shipsLogic)
    {
        this.services = services;
        NpcFleetSpawner = npcFleetSpawner;
        tickNpcFleet = npcFleetSpawner.Tick;
        this.shipsLogic = shipsLogic;
        // Fires in NavalShipsLogic.OnEndMission before it removes every hull, so the ropes are still attached. It runs before
        // the battle controller leaves (NavalShipsLogic is an earlier behavior), so the last fates precede MissionLeft.
        shipsLogic.MissionEndEvent += services.OnMissionEnding;
        // Also fires for every hull as NavalShipsLogic.OnEndMission removes them, after the final ropes went out.
        shipsLogic.BeforeShipRemovedEvent += OnBeforeShipRemoved;
        shipsLogic.ShipRemovedEvent += OnShipRemoved;
        CoopNavalMissionScope.IsActive = true;
    }

    public IBattleShipReplicator ShipReplicator => services.ShipReplicator;

    public IAgentStationUseReplicator StationUseReplicator => services.StationUseReplicator;

    public INavalDeckPoses DeckPoses => services.DeckPoses;

    public INavalNpcFleetSpawner NpcFleetSpawner { get; }

    public INavalMigrationReserveFielder MigrationReserveFielder => services.MigrationReserveFielder;

    public IBattleShipDamageRouter ShipDamageRouter => services.ShipDamageRouter;

    public IBattleShipWeaponReplicator ShipWeaponReplicator => services.ShipWeaponReplicator;

    public INavalShipEngine Engine => services.Engine;

    public IBattleShipFateReporter FateReporter => services.FateReporter;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        // A snap teleports the hull and the agents on it, so it runs here rather than inside the fixed physics step.
        services.Engine.SnapCopies();
        services.Tick(dt, tickNpcFleet);
        LogAgentCountDrop();
    }

    // Copies are steered with the physics step, so a velocity change lands on the body before it integrates.
    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        services.Engine.TickCopies(fixedDt);
    }

    // Runs after DefaultNavalMissionLogic's deployment finish, so this is the hull set the battle starts with.
    public override void OnDeploymentFinished()
    {
        base.OnDeploymentFinished();
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

    private void OnBeforeShipRemoved(MissionShip ship)
    {
        bool retreatOrdered = ship.IsShipOrderActive && ship.ShipOrder.MovementOrderEnum == ShipOrder.ShipMovementOrderEnum.Retreat;
        var reason = retreated || retreatOrdered ? ShipRemovalReason.Retreat : ShipRemovalReason.Leave;
        services.OnBeforeHullRemoved(ship, reason, shipsLogic.IsMissionEnding);
    }

    // A removed hull is pooled during deployment and its entity removed later, so no copy reference may outlive it.
    private void OnShipRemoved(MissionShip ship)
    {
        bool wasCopy = NavalForeignHulls.Contains(ship);
        services.Engine.ForgetHull(ship);
        if (shipsLogic.IsMissionEnding) return;

        Logger.Information("[NavalSync] Hull {Index} on {Team} formation {Formation} removed (copy {Copy}, pooled {Pooled})",
            ship.Index, ship.Team?.TeamSide, ship.Formation?.FormationIndex, wasCopy, shipsLogic.IsDeploymentMode);
    }

    internal static bool IsAgentCountDrop(int before, int after) => before >= 0 && before - after >= AgentDropThreshold;

    private void LogAgentCountDrop()
    {
        int count = Mission.Agents.Count;
        if (IsAgentCountDrop(lastAgentCount, count))
            Logger.Warning("[NavalSync] Active agents fell from {Before} to {After} in one tick (deployment mode {DeploymentMode})",
                lastAgentCount, count, shipsLogic.IsDeploymentMode);
        lastAgentCount = count;
    }

    public override void OnEndMissionInternal()
    {
        CoopNavalMissionScope.IsActive = false;
        shipsLogic.MissionEndEvent -= services.OnMissionEnding;
        shipsLogic.BeforeShipRemovedEvent -= OnBeforeShipRemoved;
        shipsLogic.ShipRemovedEvent -= OnShipRemoved;
        services.Dispose();
        NavalRopes.Clear();
#if DEBUG
        NavalHelmOverride.Clear();
        NavalContactProbe.Clear();
#endif
        // The static mirror's reset for a copy the engine never forgot; the engine's own entries die with the mission.
        NavalForeignHulls.Clear();
        NavalShipDamageGate.Clear();
        NavalPlayerDeploymentSlot.Reset();
        base.OnEndMissionInternal();
    }
}
