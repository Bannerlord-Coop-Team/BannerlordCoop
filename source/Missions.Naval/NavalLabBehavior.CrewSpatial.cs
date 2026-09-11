#if DEBUG
using System;
using System.Collections.Generic;
using Common;
using Missions.Battles;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal object InspectCrewSpatial()
    {
        if (!GameThread.Instance.IsGameThread) return new { unavailable = "not_game_thread" };
        if (!IsTwoClientNative) return new { unavailable = "wrong_mode" };
        if (Mission == null || Mission != Mission.Current || factoryTerminal || !nativeDeploymentComplete)
            return new { unavailable = "mission_lifetime_or_deployment" };
        var rows = new List<object>();
        for (int slot = 0; slot < manifest.Ships.Length; slot++)
        {
            if (!appliedStations.TryGetValue(slot, out var stations)) continue;
            try
            {
                var inventory = StationInventory(slot);
                for (int crew = 0; crew < 4; crew++)
                    rows.Add(InspectCrewStation(slot, crew, stations.Keys[crew], inventory[stations.Keys[crew]]));
            }
            catch (Exception exception) { rows.Add(new { slot, unavailable = exception.GetType().FullName }); }
        }
        return new
        {
            sampledUtcTicks = DateTime.UtcNow.Ticks, localMissionTick = nativeHelmTicks,
            rows = rows.ToArray(), rowLimit = 8,
            measurement = "Native getters sampled on the game thread, not an atomic physics/render cut. Stepped root and occupancy are not contact proof."
        };
    }

    private object InspectCrewStation(int slot, int crew, string key, ShipOarMachine machine)
    {
        int index = (slot * NavalLabManifest.CrewPerShip) + crew + 1;
        var id = manifest.Combatants[index];
        try
        {
            var ship = Ships[slot];
            var agent = Agents[index];
            var point = machine.PilotStandingPoint;
            if (agent == null || agent.Pointer == UIntPtr.Zero || agent.Mission != Mission || !agent.IsActive()
                || !ship.GameEntity.IsValid || !machine.GameEntity.IsValid || point == null || !point.GameEntity.IsValid)
                return new { slot, key, combatantId = id, unavailable = "agent_or_station_lifetime" };
            var frame = point.GetUserFrameForAgent(agent);
            return new
            {
                slot, shipId = manifest.Ships[slot], key, combatantId = id,
                ownerLocal = manifest.Controllers[slot] == ownControllerId,
                actor = InspectAgent(agent, index), identity = InspectHelmAgent(agent),
                nativeController = agent.Controller.ToString(), agent.IsAIControlled, agent.IsPaused,
                agentFrame = new NavalLabFrameSnapshot(agent.Frame),
                visualPosition = new NavalLabVectorSnapshot(agent.VisualPosition),
                targetPosition = new NavalLabVectorSnapshot(agent.GetTargetPosition().ToVec3()),
                movementLockedState = agent.MovementLockedState.ToString(),
                scriptedFlags = agent.GetScriptedFlags().ToString(),
                channels = new[] { InspectCrewAction(agent, 0), InspectCrewAction(agent, 1) },
                pointId = point.Id.Id, pointCreatedAtRuntime = point.Id.CreatedAtRuntime,
                machineId = machine.Id.Id, machineCreatedAtRuntime = machine.Id.CreatedAtRuntime,
                pointFrame = new NavalLabFrameSnapshot(point.GameEntity.GetGlobalFrame()),
                userFrame = new NavalLabFrameSnapshot(new MatrixFrame(frame.Rotation, frame.Origin.GetVec3WithoutValidity())),
                pointUser = InspectHelmAgent(point.UserAgent), pilot = InspectHelmAgent(machine.PilotAgent),
                usedObjectMatchesPoint = agent.CurrentlyUsedGameObject == point,
                pilotMatchesActor = machine.PilotAgent == agent,
                lastPilotMatchesActor = machine._lastPilotAgent == agent,
                nativeSittingBookkeeping = machine._isPilotSitting,
                point.LockUserFrames, point.LockUserPositions, point.TranslateUser,
                userPositionsUpdatedInMachineTick = point.GetIsUserPositionsUpdatedInTheMachineTick(),
                pointFrameChanged = point.GameEntity.GetHasFrameChanged(),
                oarOwnerMatchesShip = machine._oar?.OwnerShip == ship,
                oarExtracted = machine._oar?.IsExtracted,
                oarRowing = machine._oar?.IsInRowingMotion()
            };
        }
        catch (Exception exception) { return new { slot, key, combatantId = id, unavailable = exception.GetType().FullName }; }
    }

    private NavalLabActionSnapshot InspectCrewAction(Agent agent, int channel) => new NavalLabActionSnapshot(
        channel, agent.GetCurrentAction(channel).Index, agent.GetCurrentActionType(channel).ToString(),
        agent.GetCurrentActionProgress(channel), agent.GetActionChannelWeight(channel),
        agent.GetCurrentAnimationFlag(channel).ToString());
}
#endif
