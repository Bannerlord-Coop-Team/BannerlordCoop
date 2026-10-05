#if DEBUG
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>[Game thread] The registered hulls of the local coop naval battle, for the DEBUG naval cheats.</summary>
public interface INavalDebugHulls
{
    /// <summary>Why the local battle has no own hull to drive, or null with the hull this client owns and plays.</summary>
    string TryGetOwnHull(out MissionShip hull);

    /// <summary>The registered copy of another owner's hull nearest to <paramref name="own"/>, or null.</summary>
    MissionShip NearestForeignHull(MissionShip own);

    /// <summary>The hull's network ship id, or Guid.Empty when it is not registered.</summary>
    Guid ShipIdOf(MissionShip hull);

    /// <summary>The hull's rope throw stations, in a stable content-path order.</summary>
    ShipAttachmentMachine[] RopeStations(MissionShip hull);

    /// <summary>The hull's rope attachment points, in a stable content-path order.</summary>
    ShipAttachmentPointMachine[] AttachmentPoints(MissionShip hull);
}

/// <inheritdoc cref="INavalDebugHulls"/>
public class NavalDebugHulls : INavalDebugHulls
{
    private static CoopBattleController Controller => Mission.Current?.GetMissionBehavior<CoopBattleController>();

    public string TryGetOwnHull(out MissionShip hull)
    {
        hull = null;
        var controller = Controller;
        var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
        if (controller == null || shipsLogic == null) return "No active coop naval battle mission.";

        var own = controller.MissionComponent.ShipRegistry.Ships
            .Where(ship => ship.CurrentAuthority == controller.Session.OwnControllerId)
            .Select(ship => ship.Hull as MissionShip)
            .Where(ship => ship != null && !NavalForeignHulls.Contains(ship))
            .ToArray();
        if (own.Length == 0) return "This client owns no registered hull.";

        hull = own.Length == 1 ? own[0] : own.FirstOrDefault(ship => ship.IsPlayerShip);
        return hull == null ? "This client owns several hulls and none is its player ship." : null;
    }

    public MissionShip NearestForeignHull(MissionShip own) => NavalForeignHulls.All
        .Where(ship => ShipIdOf(ship) != Guid.Empty)
        .OrderBy(ship => ship.GlobalFrame.origin.DistanceSquared(own.GlobalFrame.origin))
        .FirstOrDefault();

    public Guid ShipIdOf(MissionShip hull)
    {
        var controller = Controller;
        return controller != null && controller.MissionComponent.ShipRegistry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;
    }

    public ShipAttachmentMachine[] RopeStations(MissionShip hull) => hull.AttachmentMachines
        .OrderBy(machine => NavalShipEngine.EntityPath(machine.GameEntity, hull.GameEntity), StringComparer.Ordinal)
        .ToArray();

    public ShipAttachmentPointMachine[] AttachmentPoints(MissionShip hull) => hull.AttachmentPointMachines
        .OrderBy(point => NavalShipEngine.EntityPath(point.GameEntity, hull.GameEntity), StringComparer.Ordinal)
        .ToArray();

    /// <summary>Where a hook lands on <paramref name="point"/>, as vanilla's throw measures it.</summary>
    internal static Vec3 HookPosition(ShipAttachmentPointMachine point) =>
        point.GameEntity.GetGlobalFrame().TransformToParent(point.HookAttachLocalPosition);
}
#endif
