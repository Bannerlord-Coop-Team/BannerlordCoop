#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Battles;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    // Vanilla follow-me spaces ships 20 m apart; the follower keeps to the outside of the flagship row.
    internal const float FleetFollowLateral = 20f;
    // Vanilla ShipOrder.SetShipFollowOrder always trails the leader by 15 m.
    internal const float FleetFollowTrail = -15f;
    // Oars need a seated pilot to produce thrust, so secondary hulls get owner-local rowers that are never registered or replicated.
    private readonly List<Agent> fleetRowers = new();
    internal IReadOnlyList<Agent> FleetRowers => fleetRowers;

    internal bool IsSecondaryHull(int slot) => slot >= 0 && slot < manifest.Ships.Length && !manifest.IsFlagship(slot);
    private bool IsOwnedSecondaryHull(int slot) => IsSecondaryHull(slot) && OwnsFactoryHull(slot);
    private MissionShip FlagshipOf(int slot) => manifest.OwnerOf(slot) < Ships.Length ? Ships[manifest.OwnerOf(slot)] : null;
    internal float FleetFollowOffset(int slot) => manifest.OwnerOf(slot) == 0 ? -FleetFollowLateral : FleetFollowLateral;

    private void SpawnFleetRowers(int slot, Team team, NavalAgentsLogic agentsLogic)
    {
        // Deck frame 0 is the captain spot on flagships; secondary hulls have no captain.
        for (int j = 1; j < NavalLabManifest.CrewPerShip; j++)
            fleetRowers.Add(SpawnFixtureAgent(slot, j, team, true, AgentControllerType.AI, agentsLogic));
    }

    internal string RequestFleetOrder(int slot, bool follow)
    {
        if (!IsOwnedSecondaryHull(slot) || slot >= Ships.Length) return "rejected:not_owned_secondary_hull";
        if (!CanUseNativeControls) return "rejected:native_controls_not_ready";
        var ship = Ships[slot];
        var flagship = FlagshipOf(slot);
        if (ship?.ShipOrder == null || flagship == null || !ship.IsAIControlled) return "rejected:ai_captain_unavailable";
        if (follow) ship.ShipOrder.SetShipFollowOrder(flagship, FleetFollowOffset(slot));
        else ship.ShipOrder.SetShipStopOrder();
        return "applied:" + FleetOrderName(ship.ShipOrder.MovementOrderEnum);
    }

    private void StopFleetOrders()
    {
        for (int slot = 0; slot < Ships.Length; slot++)
        {
            var order = IsOwnedSecondaryHull(slot) && Ships[slot]?.IsAIControlled == true ? Ships[slot].ShipOrder : null;
            if (order != null && order.MovementOrderEnum != ShipOrder.ShipMovementOrderEnum.Stop) order.SetShipStopOrder();
        }
    }

    // Follow aliases StaticOrderCount (3), so ToString alone is ambiguous.
    private static string FleetOrderName(ShipOrder.ShipMovementOrderEnum order) =>
        order == ShipOrder.ShipMovementOrderEnum.Follow ? "Follow" : order.ToString();

    internal object InspectFleet() => new
    {
        manifest.HullsPerParticipant, shipOwners = manifest.ShipOwners, ownRowers = fleetRowers.Count,
        replication = "secondary hulls send frame and sail only; orders, rowers and oar presentation stay on the owner",
        ownedSecondaryHulls = Enumerable.Range(0, Ships.Length).Where(IsOwnedSecondaryHull).Select(InspectFleetHull).ToArray()
    };

    private object InspectFleetHull(int slot)
    {
        try
        {
            var ship = Ships[slot];
            var flagship = FlagshipOf(slot);
            if (ship == null || flagship == null || !ship.GameEntity.IsValid || !flagship.GameEntity.IsValid)
                return new { slot, unavailable = "ship_unavailable" };
            // Flagship-local offset: x is starboard, y is ahead; bearing 0 is dead ahead, 90 starboard.
            var local = flagship.GlobalFrame.TransformToLocal(ship.GlobalFrame.origin);
            var order = ship.ShipOrder;
            return new
            {
                slot, flagshipSlot = manifest.OwnerOf(slot),
                controller = ship.Controller?.ControllerType.ToString() ?? "None",
                movementOrder = order == null ? null : FleetOrderName(order.MovementOrderEnum),
                orderTargetIsFlagship = order?.TargetShip == flagship,
                aiControllable = order?.IsAIControllable,
                aiHasTarget = ship.IsAIControlled ? ship.AIController.HasTarget : (bool?)null,
                distanceToFlagship = local.AsVec2.Length,
                bearingFromFlagshipDegrees = MathF.Atan2(local.x, local.y) * 180f / MathF.PI,
                flagshipLocalOffset = new[] { local.x, local.y },
                followOffset = new[] { FleetFollowOffset(slot), FleetFollowTrail },
                speed = ship.Physics?.LinearVelocity.Length,
                seatedRowers = ship.ShipOarMachines.Count(machine => machine.PilotAgent != null && fleetRowers.Contains(machine.PilotAgent))
            };
        }
        catch (Exception exception) { return new { slot, error = exception.GetType().FullName }; }
    }
}
#endif
