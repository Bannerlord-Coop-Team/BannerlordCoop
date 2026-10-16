#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.NavalScenarios;
using GameInterface.Services.ObjectManager;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.scenario_status
/// <summary>DEBUG: reports the naval scenario on this machine: preset, phase, steps and, in its battle, the hull counts and result.</summary>
public sealed class NavalScenarioStatusCoopCommand : ICoopCommand
{
    private readonly INavalScenarioState state;
    private readonly IObjectManager objectManager;

    public NavalScenarioStatusCoopCommand(INavalScenarioState state, IObjectManager objectManager)
    {
        this.state = state;
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "scenario_status";

    public string Description =>
        "Reports the naval scenario's preset, phase, pending steps, hulls per owner, AI hulls, adoption and hand-back phases and result.";

    public CoopCommandSide Side => CoopCommandSide.Both;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var config = state.Current;
        if (config == null) return new CoopCommandResult(true, "NAVAL_SCENARIO_STATUS none");

        if (ModInformation.IsServer) AdvanceServerPhase(config);

        var status = new
        {
            preset = config.Preset,
            scenario = config.ScenarioId,
            mapEvent = config.MapEventId,
            phase = state.Phase.ToString(),
            players = config.ControllerIds,
            knobs = new
            {
                ships = config.ShipsPerPlayer,
                enemyHulls = config.EnemyHulls,
                hull = config.HullId,
                placement = config.Placement.ToString(),
                spacing = config.Spacing,
                delay = config.DelaySeconds,
                reconnect = config.ReconnectSeconds,
                who = config.DisconnectBattleHost ? config.DisconnectNonHost ? "nonhost" : "host" : null,
            },
            pending = state.Steps.Where(step => step.Status == NavalScenarioStepStatus.Pending).Select(step => step.Name).ToArray(),
            steps = state.Steps.Select(step => new { name = step.Name, status = step.Status.ToString(), detail = step.Detail }).ToArray(),
            battle = ModInformation.IsServer ? null : InspectBattle(config),
        };

        return new CoopCommandResult(true, "NAVAL_SCENARIO_STATUS " + JsonConvert.SerializeObject(status));
    }

    // The server runs no mission, so its phase follows the map event: claimed for a mission, then finalized.
    private void AdvanceServerPhase(NavalScenarioConfig config)
    {
        if (!objectManager.TryGetObject(config.MapEventId, out MapEvent mapEvent) || mapEvent.IsFinalized)
            state.Advance(NavalScenarioPhase.Ended);
        else if (ServerBattleModeArbiter.IsMissionClaimed(config.MapEventId))
            state.Advance(NavalScenarioPhase.Fighting);
    }

    // The counters coop.debug.naval.inspect reports, for this client's copy of the scenario's battle.
    private static object InspectBattle(NavalScenarioConfig config)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || controller.Session.InstanceId != config.MapEventId) return null;

        var ships = controller.MissionComponent.ShipRegistry.Ships.ToArray();
        var npcHulls = ships.Where(ship => ship.IsNpcParty).ToArray();
        return new
        {
            isHost = controller.Session.IsLocalHost,
            deploymentCommitted = controller.Deployment.IsCommitted,
            hullsPerOwner = ships.Where(ship => !ship.IsNpcParty)
                .GroupBy(ship => ship.CurrentAuthority ?? "none")
                .ToDictionary(group => group.Key, group => group.Count()),
            aiHulls = new
            {
                total = npcHulls.Length,
                sinkingOrSunk = npcHulls.Count(ship => (ship.Hull as MissionShip)?.IsSinking == true),
                ownedHere = npcHulls.Count(ship => ship.CurrentAuthority == controller.Session.OwnControllerId),
            },
            // Each player hull with its owner, adopted by the host or handed back, and this client's pending reclaims.
            handBack = mission.GetMissionBehavior<CoopNavalBattleBehavior>()?.ShipReplicator.InspectHandBack(),
            // How the copies exist here and how close every hull pair came, for the ramming preset above all.
            copyModes = NavalCopyBodies.Table.Entries.Select(entry => entry.Value.ShipId.ToString("N").Substring(0, 8) + "=" +
                entry.Value.Mode.ToString().ToLowerInvariant()).ToArray(),
            closestApproach = NavalContactProbe.InspectApproaches(),
            result = mission.MissionEnded ? CoopNavalBattleEndLogic.DescribeResult(mission.MissionResult) : "running",
        };
    }
}
#endif
