using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using TaleWorlds.CampaignSystem.Naval;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Reports the local coop naval battle: session, reserve guard outcome and per-hull state.</summary>
public sealed class NavalInspectCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "inspect";

    public string Description => "Reports the coop naval battle session and hull state on this client.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var shipsLogic = mission?.GetMissionBehavior<NavalShipsLogic>();
        if (controller == null || shipsLogic == null)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");

        var state = new
        {
            instance = controller.Session.InstanceId,
            ownController = controller.Session.OwnControllerId,
            isHost = controller.Session.IsLocalHost,
            deploymentCommitted = controller.Deployment.IsCommitted,
            ownReservePresent = mission.GetMissionBehavior<CoopNavalReserveGuard>()?.ReservePresent,
            shipSync = mission.GetMissionBehavior<CoopNavalBattleBehavior>()?.ShipReplicator.Inspect(),
            shipDamage = mission.GetMissionBehavior<CoopNavalBattleBehavior>()?.ShipDamageRouter.Inspect(),
            shipDamageGate = NavalShipDamageGate.Inspect(),
            shipDecks = controller.MissionComponent.AgentMovementHandler.InspectShipDecks(),
            hostEpoch = controller.Session.HostEpoch,
            npcFleet = InspectNpcFleet(controller, mission.GetMissionBehavior<CoopNavalBattleBehavior>()),
            stationUse = mission.GetMissionBehavior<CoopNavalBattleBehavior>()?.StationUseReplicator.Inspect(),
            agents = InspectAgents(mission, controller.MissionComponent, mission.GetMissionBehavior<CoopNavalBattleBehavior>()),
            mainAgent = Agent.Main?.Character?.StringId,
            mainAgentUsing = Agent.Main?.CurrentlyUsedGameObject is UsableMissionObject used ? used.GameEntity.Name : null,
            ships = shipsLogic.AllShips.Select(ship => new
            {
                hull = ship.ShipOrigin.Hull?.StringId,
                origin = ship.ShipOrigin.GetType().Name,
                detachedFromParty = ship.ShipOrigin is Ship campaignShip ? (bool?)(campaignShip.Owner?.Ships.Contains(campaignShip) != true) : null,
                team = ship.Team?.TeamSide.ToString(),
                formation = ship.Formation?.FormationIndex.ToString(),
                isPlayerShip = ship.IsPlayerShip,
                hasPlayerStandingPoint = ship.HasPlayerStandingPointEntity,
                captain = ship.Captain?.Character?.StringId,
                helm = ship.ShipControllerMachine?.PilotAgent?.Character?.StringId,
                controller = ship.Controller?.ControllerType.ToString() ?? "None",
                activeBody = ship.GameEntity.IsValid ? (bool?)ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation() : null,
                foreign = NavalForeignHulls.Contains(ship),
                hitPoints = ship.HitPoints,
                maxHitPoints = ship.ShipOrigin.MaxHitPoints,
                position = ship.GlobalFrame.origin.ToString(),
            }).ToArray(),
        };

        return new CoopCommandResult(true, "NAVAL_INSPECT " + JsonConvert.SerializeObject(state));
    }

    // AI hulls with their authority and how many have a pilot at the helm (the AI captain steers through it).
    private static object InspectNpcFleet(CoopBattleController controller, CoopNavalBattleBehavior naval)
    {
        var hulls = controller.MissionComponent.ShipRegistry.Ships.Where(ship => ship.IsNpcParty).ToArray();
        return new
        {
            spawner = naval?.NpcFleetSpawner.Inspect(),
            isLocalHost = controller.Session.IsLocalHost,
            hulls = hulls.Length,
            ownedHere = hulls.Count(ship => ship.CurrentAuthority == controller.Session.OwnControllerId),
            helmPilotsSeated = hulls.Count(ship => (ship.Hull as MissionShip)?.ShipControllerMachine?.PilotAgent != null),
            aiControllers = hulls.Count(ship => (ship.Hull as MissionShip)?.Controller?.ControllerType == NavalDLC.Missions.ShipControl.ShipControllerType.AI),
        };
    }

    // Registered human agents with the hull whose formation they serve on, so a puppet's hull is visible.
    private static object[] InspectAgents(Mission mission, ICoopMissionComponent missionComponent, CoopNavalBattleBehavior naval) => mission.Agents
        .Where(agent => agent.IsHuman && agent.IsActive())
        .Select(agent =>
        {
            missionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info);
            missionComponent.ShipRegistry.TryGetByFormation(agent.Formation, out var ship);
            Guid deckShip = Guid.Empty;
            Vec3 deckLocal = Vec3.Zero;
            bool onDeck = naval != null && naval.ShipReplicator.TryGetDeckPose(agent, agent.Position, out deckShip, out deckLocal);
            return (object)new
            {
                name = agent.Name,
                agentId = info?.AgentId,
                owner = info?.CurrentAuthority,
                team = agent.Team?.TeamSide.ToString(),
                formation = agent.Formation?.FormationIndex.ToString(),
                shipId = ship?.ShipId,
                shipOwner = ship?.CurrentAuthority,
                deckShip = onDeck ? (Guid?)deckShip : null,
                deckLocal = onDeck ? deckLocal.ToString() : null,
                station = naval?.StationUseReplicator.DescribeStation(agent),
                seatLock = agent.MovementLockedState.ToString(),
                ownSeat = info != null && naval != null && naval.StationUseReplicator.IsOwnSeat(info.AgentId),
            };
        })
        .ToArray();
}
