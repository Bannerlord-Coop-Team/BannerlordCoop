#if DEBUG
using Common.Commands;
using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;

namespace Missions.Naval;

/// <summary>Reads late-hull setup and traces the real station release path without retaining mission objects.</summary>
public interface INavalLateHullProbe
{
    /// <summary>Inspects a registered hull or stages one action on a locally controlled agent.</summary>
    CoopCommandResult Execute(string prefix, string operation, int agentIndex, int stationIndex);
    /// <summary>Logs station setup and the resulting action/equipment immediately after vanilla releases it.</summary>
    void LogRelease(Agent agent, UsableMissionObject point);
}

/// <inheritdoc cref="INavalLateHullProbe"/>
public sealed class NavalLateHullProbe : INavalLateHullProbe
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalLateHullProbe>();
    private readonly INavalDebugHulls hulls;

    /// <summary>Uses the established content ordering and network hull identity helpers.</summary>
    public NavalLateHullProbe(INavalDebugHulls hulls) => this.hulls = hulls;

    /// <summary>[Game thread] Validates authority, then starts or stops normal vanilla station use.</summary>
    public CoopCommandResult Execute(string prefix, string operation, int agentIndex, int stationIndex)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var registry = controller.MissionComponent.ShipRegistry;
        if (prefix == null) return Success(new
        {
            time = mission.CurrentTime,
            hulls = registry.Ships.Select(ship => ship.Hull).OfType<MissionShip>()
                .Where(ship => ship.GameEntity.IsValid && !ship.IsRemoved).Select(ship => Inspect(ship)).ToArray(),
            agents = mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman).Select(agent => AgentState(agent, controller)).ToArray(),
        });
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), prefix, out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull || !hull.GameEntity.IsValid || hull.IsRemoved)
            return Failed("The hull is not a live naval ship.");
        if (operation == null) return Success(Inspect(hull));
        if (!mission.IsDeploymentFinished) return Failed("Wait until deployment finishes before staging station use.");

        var agent = mission.Agents.FirstOrDefault(candidate => candidate.Index == agentIndex && candidate.IsActive() && candidate.IsHuman);
        if (agent == null) return Failed("No active human agent has that local index.");
        // Agents and hulls are simulated by clients, including the battle host's AI fleet, not the dedicated server.
        if (!controller.MissionComponent.AgentRegistry.IsLocallyControlled(agent))
            return Failed("Run the action on the agent's authority client; indices are local to each client.");

        if (operation == "release")
        {
            if (!Stations(hull, "climb").Concat(Stations(hull, "rope")).Concat(Stations(hull, "oar"))
                .Any(point => point == agent.CurrentlyUsedGameObject)) return Failed("The agent is not using a station on this hull.");
            agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
            return Success(new { operation, ship = shipId, agent = AgentState(agent, controller) });
        }

        var stations = Stations(hull, operation);
        if (stationIndex < 0 || stationIndex >= stations.Length) return Failed("No station has that index; inspect this hull first.");
        if (agent.CurrentlyUsedGameObject != null) return Failed("Release the agent's current station first.");
        var point = stations[stationIndex];
        if (point.HasUser || !point.GameEntity.IsValid) return Failed("The chosen station is occupied or removed.");
        if (operation != "climb" && (!agent.IsAIControlled || info.CurrentAuthority != controller.Session.OwnControllerId))
            return Failed("Rope and oar staging needs an AI agent on the hull authority client.");

        // Selects a station directly, bypassing walking and interaction range, but leaves animation and cleanup to vanilla.
        agent.HandleStartUsingAction(point, -1);
        if (agent.CurrentlyUsedGameObject != point) return Failed("Vanilla refused station use.");
        return Success(new { operation, ship = shipId, station = stationIndex, agent = AgentState(agent, controller),
            note = "Wait for the normal station tick before release or hull removal." });
    }

    /// <summary>Reports setup components, current users and the ballista's cached and selected hull identities.</summary>
    private object Inspect(MissionShip hull)
    {
        var ai = hull.ShipSiegeWeapon?.Ai as RangedSiegeWeaponAi;
        return new
        {
            ship = hulls.ShipIdOf(hull), copy = NavalForeignHulls.Contains(hull), time = Mission.Current.CurrentTime,
            stations = new[] { "climb", "rope", "oar" }.Select(kind => new
            {
                kind,
                points = Stations(hull, kind).Select((point, index) => new
                {
                    index, key = NavalShipEngine.EntityPath(point.GameEntity, hull.GameEntity), user = point.UserAgent?.Index,
                    resetAnimation = point.GetComponent<ResetAnimationOnStopUsageComponent>() != null,
                    resetGravity = point.GetComponent<ResetGravityExclusionAndEntityAttachmentOnStopUsageComponent>() != null,
                    removeExtraWeapon = point.GetComponent<RemoveExtraWeaponOnStopUsageComponent>() != null,
                    resetAction = point.GetComponent<ResetAnimationOnStopUsageComponent>()?._successfulResetAction.Index,
                    resetActionName = point.GetComponent<ResetAnimationOnStopUsageComponent>()?._successfulResetAction.GetName(),
                }).ToArray(),
            }).ToArray(),
            climbers = hull.ClimbingMachines.Select(machine => new
            {
                key = NavalShipEngine.EntityPath(machine.GameEntity, hull.GameEntity),
                usageSeconds = machine._standingPointUsageDurations.ToArray(),
                attachedAgents = Mission.Current.Agents.Where(agent => agent.IsActive() && agent.IsHuman &&
                    agent.IsAgentParentEntitySameAs(GameEntity.CreateFromWeakEntity(machine.GameEntity)))
                    .Select(agent => agent.Index).ToArray(),
            }).ToArray(),
            ballista = ai == null ? null : new
            {
                cachedHullIds = TargetHullIds(ai._threatSeeker._potentialTargetObjects),
                selectedHullId = TargetHullId(ai._target?.TargetableObject),
            },
        };
    }

    /// <summary>Resolves vanilla's ship target wrapper to its registry hull identity; other target types have no hull id.</summary>
    internal Guid? TargetHullId(ITargetable target) => target is ShipTargetMissionObject shipTarget ? hulls.ShipIdOf(shipTarget._ship) : null;

    /// <summary>Preserves an uninitialized cache as null and reports each cached ship once.</summary>
    internal Guid[] TargetHullIds(IEnumerable<ITargetable> targets) => targets?.OfType<ShipTargetMissionObject>()
        .Select(target => TargetHullId(target).Value).Distinct().ToArray();

    /// <summary>Uses stable content paths; climb indices flatten each machine's standing points.</summary>
    private StandingPoint[] Stations(MissionShip hull, string kind)
    {
        if (kind == "climb") return hull.ClimbingMachines.OrderBy(machine => NavalShipEngine.EntityPath(machine.GameEntity, hull.GameEntity), StringComparer.Ordinal)
            .SelectMany(machine => machine.StandingPoints).ToArray();
        if (kind == "rope") return hulls.RopeStations(hull).Select(machine => machine.PilotStandingPoint).Where(point => point != null).ToArray();
        if (kind == "oar") return hull.ShipOarMachines
            .OrderBy(machine => NavalShipEngine.EntityPath(machine.GameEntity, hull.GameEntity), StringComparer.Ordinal)
            .Select(machine => machine.PilotStandingPoint).Where(point => point != null).ToArray();
        return Array.Empty<StandingPoint>();
    }

    /// <summary>Includes network identity to compare the same agent despite different local indices.</summary>
    private object AgentState(Agent agent, CoopBattleController controller)
    {
        controller.MissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info);
        return new { index = agent.Index, id = info?.AgentId, authority = info?.CurrentAuthority,
            ai = agent.IsAIControlled, usingStation = agent.CurrentlyUsedGameObject?.GameEntity.Name,
            action0 = agent.GetCurrentAction(0).Index, action1 = agent.GetCurrentAction(1).Index,
            action0Name = agent.GetCurrentAction(0).GetName(), action1Name = agent.GetCurrentAction(1).GetName(),
            climbing = agent.GetCurrentAction(0) == ActionIndexCache.Create("act_climb_net") ||
                agent.GetCurrentAction(0) == ActionIndexCache.Create("act_climb_net_ending") ||
                agent.GetCurrentAction(0) == ActionIndexCache.Create("act_climb_net_ending_continue"),
            extraWeapon = agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Item?.StringId,
            climbParentShips = controller.MissionComponent.ShipRegistry.Ships.Where(ship => ship.Hull is MissionShip hull &&
                hull.GameEntity.IsValid && !hull.IsRemoved && hull.ClimbingMachines.Any(machine => machine.GameEntity.IsValid &&
                    agent.IsAgentParentEntitySameAs(GameEntity.CreateFromWeakEntity(machine.GameEntity)))).Select(ship => ship.ShipId).ToArray(),
            position = new { x = agent.Position.x, y = agent.Position.y, z = agent.Position.z } };
    }

    /// <summary>Records the immediate release result before a later action packet can replace its animation.</summary>
    public void LogRelease(Agent agent, UsableMissionObject point)
    {
        var controller = agent.Mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null) return;
        foreach (var info in controller.MissionComponent.ShipRegistry.Ships)
        {
            if (info.Hull is not MissionShip hull || !hull.GameEntity.IsValid) continue;
            foreach (string kind in new[] { "climb", "rope", "oar" })
            {
                if (!Stations(hull, kind).Contains(point)) continue;
                Logger.Information("NAVAL_LATE_HULL_RELEASE {State}", JsonConvert.SerializeObject(new
                {
                    time = agent.Mission.CurrentTime,
                    station = new { ship = info.ShipId, copy = NavalForeignHulls.Contains(hull), kind,
                        key = NavalShipEngine.EntityPath(point.GameEntity, hull.GameEntity),
                        expectedResetAction = point.GetComponent<ResetAnimationOnStopUsageComponent>()?._successfulResetAction.Index,
                        expectedResetActionName = point.GetComponent<ResetAnimationOnStopUsageComponent>()?._successfulResetAction.GetName(),
                        resetGravity = point.GetComponent<ResetGravityExclusionAndEntityAttachmentOnStopUsageComponent>() != null,
                        removeExtraWeapon = point.GetComponent<RemoveExtraWeaponOnStopUsageComponent>() != null },
                    after = AgentState(agent, controller),
                }));
                return;
            }
        }
    }

    /// <summary>Emits machine-readable local state.</summary>
    private CoopCommandResult Success(object state) => new CoopCommandResult(true, "NAVAL_LATE_HULL " + JsonConvert.SerializeObject(state));

    /// <summary>Reports a refusal before any station action.</summary>
    private CoopCommandResult Failed(string error) => new CoopCommandResult(false, error, "command_failed");
}
#endif
