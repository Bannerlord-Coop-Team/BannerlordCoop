#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Stages a selected owned hull for removal by vanilla's map-edge retreat tick.</summary>
public interface INavalEdgeRetreatCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalEdgeRetreatCoopCommand"/>
public sealed class NavalEdgeRetreatCoopCommand : INavalEdgeRetreatCoopCommand
{
    private readonly INavalEdgeRetreatStaging staging;

    /// <summary>Uses boundary geometry without introducing a second removal path.</summary>
    public NavalEdgeRetreatCoopCommand(INavalEdgeRetreatStaging staging) => this.staging = staging;

    public string Prefix => "coop.debug.naval";
    public string Name => "edge_retreat";
    public string Description => "Orders an owned hull to retreat at the map edge; vanilla removes it on its next five-second check.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "Owned hull id from inspect summary.", true),
    };

    /// <summary>Runs on the hull authority client because the dedicated server does not simulate hulls, and copies cannot order retreat.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1) return Failed("Usage: coop.debug.naval.edge_retreat <shipId-prefix>");
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var ships = mission?.GetMissionBehavior<NavalShipsLogic>();
        if (controller == null || ships == null || mission.GetMissionBehavior<ShipRetreatLogic>() == null)
            return Failed("No active coop naval battle mission with vanilla retreat logic.");
        if (!mission.IsDeploymentFinished) return Failed("Finish deployment before staging a retreat.");
        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(info => info.ShipId), args[0], out var id);
        if (error != null) return Failed(error);
        var info = registry.Ships.Single(entry => entry.ShipId == id);
        if (info.CurrentAuthority != controller.Session.OwnControllerId)
            return Failed("Run on the selected hull's authority client.");
        var ship = info.Hull as MissionShip;
        if (ship == null || ship.IsRemoved || ship.IsSinking || !ship.IsShipOrderActive || NavalForeignHulls.Contains(ship))
            return Failed("Select an active, afloat owned hull.");
        if (ship.Formation == null) return Failed("Select a hull with an assigned formation.");
        if (ship.GetIsConnected()) return Failed("Disconnect ropes before staging a retreat.");
        var origin = ship.GlobalFrame.origin;
        if (!mission.IsPositionInsideBoundaries(origin.AsVec2)) return Failed("Hull must start inside the mission boundary.");
        var boundary = mission.GetClosestBoundaryPosition(origin.AsVec2);
        if (origin.AsVec2.DistanceSquared(boundary) < 0.0001f) return Failed("Hull is already on the boundary.");
        var frame = staging.AtEdge(origin, boundary);
        if (!mission.IsPositionInsideBoundaries(frame.origin.AsVec2)) return Failed("Could not stage just inside this boundary.");

        NavalHelmOverride.Clear();
        ship.SetAnchor(false);
        ships.TeleportShip(ship, frame, checkFreeArea: false);
        ship.GameEntity.SetAngularVelocity(Vec3.Zero);
        ship.GameEntity.SetLinearVelocity(Vec3.Zero);
        // Formation behaviors otherwise replace Retreat with their next naval movement order.
        ship.Formation.SetControlledByAI(false);
        ship.ShipOrder.SetShipRetreatOrder();
        ship.ShipOrder.SetCutLoose(enable: true);
        return new CoopCommandResult(true, $"NAVAL_EDGE_RETREAT ship={id} authority={info.CurrentAuthority} order={ship.ShipOrder.MovementOrderEnum} inside=0.25 formationAI={ship.Formation.IsAIControlled}; staged, vanilla removal pending on next five-second retreat check");
    }

    /// <summary>Reports a refused request before staging the hull.</summary>
    private static CoopCommandResult Failed(string error) => new CoopCommandResult(false, error, "command_failed");
}
#endif
