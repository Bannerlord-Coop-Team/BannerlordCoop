using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.Engine;
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
            mainAgent = Agent.Main?.Character?.StringId,
            mainAgentUsing = Agent.Main?.CurrentlyUsedGameObject is UsableMissionObject used ? used.GameEntity.Name : null,
            ships = shipsLogic.AllShips.Select(ship => new
            {
                hull = ship.ShipOrigin.Hull?.StringId,
                origin = ship.ShipOrigin is CoopShipOrigin ? "coop" : ship.ShipOrigin.GetType().Name,
                team = ship.Team?.TeamSide.ToString(),
                formation = ship.Formation?.FormationIndex.ToString(),
                isPlayerShip = ship.IsPlayerShip,
                hasPlayerStandingPoint = ship.HasPlayerStandingPointEntity,
                captain = ship.Captain?.Character?.StringId,
                helm = ship.ShipControllerMachine?.PilotAgent?.Character?.StringId,
                controller = ship.Controller?.ControllerType.ToString() ?? "None",
                activeBody = ship.GameEntity.IsValid ? (bool?)ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation() : null,
                hitPoints = ship.HitPoints,
                maxHitPoints = ship.ShipOrigin.MaxHitPoints,
                position = ship.GlobalFrame.origin.ToString(),
            }).ToArray(),
        };

        return new CoopCommandResult(true, "NAVAL_INSPECT " + JsonConvert.SerializeObject(state));
    }
}
