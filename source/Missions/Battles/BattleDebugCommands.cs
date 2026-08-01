using Common;
using GameInterface;
using GameInterface.Services.MapEvents;
#if DEBUG
using Missions.Diagnostics;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Battles;

/// <summary>Reports state needed to verify co-op battle synchronization.</summary>
internal static class BattleDebugCommands
{
    private static readonly Dictionary<int, Vec3> EnemyPositions = new Dictionary<int, Vec3>();
    private static Mission observedMission;
    private static Camera ladderCamera;

#if DEBUG
    private static Agent wieldTestAgent;
    private static Guid wieldTestAgentId;
    private static EquipmentIndex wieldTestOriginalMainHand;
    private static bool wieldTestActive;

    [CommandLineArgumentFunction("action_performance", "coop.debug.battle")]
    public static string ActionPerformance(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.battle.action_performance " +
                   "<start|snapshot|stop|status>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                MissionActionDiagnostics.StartPerformance();
                return "Action performance instrumentation is ON.";
            case "snapshot":
                return "ACTION_PERFORMANCE " +
                       MissionActionDiagnostics.SnapshotPerformance(
                           stop: false);
            case "stop":
                return "ACTION_PERFORMANCE " +
                       MissionActionDiagnostics.SnapshotPerformance(
                           stop: true);
            case "status":
                return "Action performance instrumentation is " +
                       (MissionActionDiagnostics.PerformanceEnabled
                           ? "ON."
                           : "OFF.");
            default:
                return "Usage: coop.debug.battle.action_performance " +
                       "<start|snapshot|stop|status>";
        }
    }

    [CommandLineArgumentFunction("animation_trace", "coop.debug.battle")]
    public static string AnimationTrace(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.battle.animation_trace " +
                   "<start|snapshot|stop|status>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                MissionActionDiagnostics.StartAnimationTrace();
                return "Battle animation trace is ON.";
            case "snapshot":
                return "BATTLE_ANIMATION_TRACE " +
                       MissionActionDiagnostics.SnapshotAnimationTrace(
                           stop: false);
            case "stop":
                return "BATTLE_ANIMATION_TRACE " +
                       MissionActionDiagnostics.SnapshotAnimationTrace(
                           stop: true);
            case "status":
                return "Battle animation trace is " +
                       (MissionActionDiagnostics.AnimationTraceEnabled
                           ? "ON."
                           : "OFF.");
            default:
                return "Usage: coop.debug.battle.animation_trace " +
                       "<start|snapshot|stop|status>";
        }
    }

    [CommandLineArgumentFunction("wield_test", "coop.debug.battle")]
    public static string WieldTest(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.battle.wield_test <start|restore|status>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                return StartWieldTest();
            case "restore":
                return RestoreWieldTest();
            case "status":
                return wieldTestActive
                    ? $"WIELD_TEST active agent={wieldTestAgentId:D}"
                    : "WIELD_TEST inactive";
            default:
                return "Usage: coop.debug.battle.wield_test <start|restore|status>";
        }
    }

    private static string StartWieldTest()
    {
        if (wieldTestActive) return "WIELD_TEST already active";
        Agent agent = Agent.Main;
        if (agent == null || !agent.IsActive() || agent.Mission != Mission.Current)
            return "WIELD_TEST no active main agent";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry)
            || !registry.TryGetAgentInfo(agent, out var info)
            || !registry.IsLocallyControlled(info.AgentId))
        {
            return "WIELD_TEST main agent is not locally controlled";
        }

        EquipmentIndex original = agent.GetPrimaryWieldedItemIndex();
        EquipmentIndex target = EquipmentIndex.None;
        if (original == EquipmentIndex.None)
        {
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots;
                 slot++)
            {
                if (agent.Equipment[slot].Item == null) continue;
                target = slot;
                break;
            }
            if (target == EquipmentIndex.None)
                return "WIELD_TEST main agent has no weapon";
        }

        wieldTestAgent = agent;
        wieldTestAgentId = info.AgentId;
        wieldTestOriginalMainHand = original;
        wieldTestActive = true;
        if (original == EquipmentIndex.None)
        {
            agent.TryToWieldWeaponInSlot(
                target,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                isWieldedOnSpawn: false);
        }
        else
        {
            agent.TryToSheathWeaponInHand(
                Agent.HandIndex.MainHand,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible);
        }
        return $"WIELD_TEST_STARTED agent={wieldTestAgentId:D} " +
            $"original={(int)original} target={(int)target}";
    }

    private static string RestoreWieldTest()
    {
        if (!wieldTestActive) return "WIELD_TEST inactive";
        if (wieldTestAgent == null
            || !wieldTestAgent.IsActive()
            || wieldTestAgent.Mission != Mission.Current)
        {
            return "WIELD_TEST agent unavailable";
        }

        if (wieldTestOriginalMainHand == EquipmentIndex.None)
        {
            wieldTestAgent.TryToSheathWeaponInHand(
                Agent.HandIndex.MainHand,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible);
        }
        else
        {
            wieldTestAgent.TryToWieldWeaponInSlot(
                wieldTestOriginalMainHand,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                isWieldedOnSpawn: false);
        }
        Guid restoredAgentId = wieldTestAgentId;
        wieldTestAgent = null;
        wieldTestAgentId = Guid.Empty;
        wieldTestOriginalMainHand = EquipmentIndex.None;
        wieldTestActive = false;
        return $"WIELD_TEST_RESTORED agent={restoredAgentId:D}";
    }
#endif

    [CommandLineArgumentFunction("state", "coop.debug.battle")]
    public static string State(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.battle.state";
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var playerTeam = mission?.PlayerTeam;
        if (mission == null || controller == null)
        {
            return "No active coop battle mission";
        }

        if (observedMission != mission)
        {
            EnemyPositions.Clear();
            ReleaseLadderCamera();
            observedMission = mission;
        }

        var enemies = new List<Agent>();
        int enemyParties = 0;
        if (playerTeam != null)
        {
            var enemySide = playerTeam.Side == BattleSideEnum.Attacker
                ? BattleSideEnum.Defender
                : BattleSideEnum.Attacker;
            enemies.AddRange(mission.Agents
                .Where(agent => agent.IsActive() && agent.IsHuman && agent.Team?.Side == enemySide));
            enemyParties = playerTeam.Side == BattleSideEnum.Attacker
                ? MobileParty.MainParty?.MapEvent?.DefenderSide?.Parties?.Count ?? 0
                : MobileParty.MainParty?.MapEvent?.AttackerSide?.Parties?.Count ?? 0;
        }

        int moved = 0;
        foreach (var enemy in enemies)
        {
            if (EnemyPositions.TryGetValue(enemy.Index, out var previous)
                && previous.DistanceSquared(enemy.Position) > 0.25f)
            {
                moved++;
            }
            EnemyPositions[enemy.Index] = enemy.Position;
        }

        bool deploymentReady = mission.GetMissionBehavior<DeploymentMissionController>()?.TeamSetupOver == true;
        int activeAgents = mission.Agents.Count(agent => agent.IsActive());
        int enemyFleeing = enemies.Count(agent => agent.IsRunningAway);
        var result = mission.MissionResult;

        return $"instance={controller.Session.InstanceId} host={controller.Session.IsLocalHost} " +
            $"activated={controller.Deployment.IsActivated} committed={controller.Deployment.IsCommitted} " +
            $"deploymentReady={deploymentReady} mainAgent={Agent.Main != null} activeAgents={activeAgents} " +
            $"playerSide={playerTeam?.Side.ToString() ?? "None"} enemyParties={enemyParties} enemyActive={enemies.Count} " +
            $"enemyAi={enemies.Count(agent => agent.IsAIControlled)} enemyFleeing={enemyFleeing} " +
            $"enemyMovedSinceLast={moved} resultState={result?.BattleState.ToString() ?? "None"} " +
            $"battleResolved={result?.BattleResolved ?? false} playerVictory={result?.PlayerVictory ?? false}";
    }

    [CommandLineArgumentFunction("ladder_state", "coop.debug.battle")]
    public static string LadderState(List<string> args)
    {
        if (args.Count > 1 || (args.Count == 1 && !int.TryParse(args[0], out _)))
        {
            return "Usage: coop.debug.battle.ladder_state [machineId]";
        }

        var mission = Mission.Current;
        if (mission == null || !mission.IsSiegeBattle)
        {
            return "No active siege mission";
        }

        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry))
        {
            return "Unable to resolve NetworkAgentRegistry";
        }

        int? selectedId = args.Count == 1 ? int.Parse(args[0]) : null;
        var ladders = mission.MissionObjects
            .OfType<SiegeLadder>()
            .Where(ladder => selectedId == null || ladder.Id.Id == selectedId.Value)
            .OrderBy(ladder => ladder.Id.Id)
            .ToArray();
        if (ladders.Length == 0)
        {
            return selectedId == null
                ? "No siege ladders are registered"
                : $"Siege ladder {selectedId.Value} was not found";
        }

        var output = new StringBuilder();
        output.AppendLine($"ladders={ladders.Length} authority={SiegeMissionAuthorityGate.IsLocalAuthority} " +
            $"known={SiegeMissionAuthorityGate.IsAuthorityKnown}");
        foreach (var ladder in ladders)
        {
            int animationIndex = ladder._ladderSkeleton.GetAnimationIndexAtChannel(0);
            float animationProgress = animationIndex >= 0
                ? ladder._ladderSkeleton.GetAnimationParameterAtChannel(0)
                : 0f;

            var users = new List<string>();
            int deactivatedPoints = 0;
            foreach (var standingPoint in ladder.StandingPoints)
            {
                if (standingPoint.IsDeactivated) deactivatedPoints++;

                var agent = standingPoint.UserAgent ?? standingPoint.MovingAgent;
                if (agent == null) continue;

                string role = standingPoint.GameEntity.HasTag(ladder.AttackerTag)
                    ? "attacker"
                    : standingPoint.GameEntity.HasTag(ladder.DefenderTag) ? "defender" : "other";
                string controller = agentRegistry.TryGetAgentInfo(agent, out var info)
                    ? info.CurrentAuthority
                    : "unregistered";
                users.Add($"{role}:{controller}:{agent.Index}");
            }

            output.AppendLine($"ladder={ladder.Id.Id:D5} state={ladder.State} animation={ladder._animationState} " +
                $"animationIndex={animationIndex} progress={animationProgress:0.000} " +
                $"simLocal={SiegeMissionAuthorityGate.IsMachineSimulatedLocally(ladder.Id.Id)} " +
                $"points={ladder.StandingPoints.Count} pointsOff={deactivatedPoints} " +
                $"users={(users.Count > 0 ? string.Join(",", users) : "none")}");
        }

        return output.ToString();
    }

    [CommandLineArgumentFunction("focus_ladder", "coop.debug.battle")]
    public static string FocusLadder(List<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int machineId))
        {
            return "Usage: coop.debug.battle.focus_ladder <machineId>";
        }

        var mission = Mission.Current;
        var ladder = mission?.MissionObjects
            .OfType<SiegeLadder>()
            .FirstOrDefault(candidate => candidate.Id.Id == machineId);
        if (ladder == null)
        {
            return $"Siege ladder {machineId} was not found";
        }

        if (!(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.CombatCamera == null)
        {
            return "The mission screen is not active";
        }

        ReleaseLadderCamera();
        ladderCamera = Camera.CreateCamera();
        ladderCamera.FillParametersFrom(missionScreen.CombatCamera);

        var frame = ladder.GameEntity.GetGlobalFrame();
        var target = frame.origin + (Vec3.Up * 2.5f);
        var position = target - (frame.rotation.f * 12f) + (Vec3.Up * 4f);
        ladderCamera.LookAt(position, target, Vec3.Up);
        missionScreen.CustomCamera = ladderCamera;

        return $"Focused the mission camera on siege ladder {machineId}";
    }

    [CommandLineArgumentFunction("release_ladder_camera", "coop.debug.battle")]
    public static string ReleaseLadderCameraCommand(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.battle.release_ladder_camera";
        }

        bool released = ReleaseLadderCamera();
        return released ? "Released the ladder camera" : "No ladder camera was active";
    }

    private static bool ReleaseLadderCamera()
    {
        if (ladderCamera == null) return false;

        if (ScreenManager.TopScreen is MissionScreen missionScreen
            && missionScreen.CustomCamera == ladderCamera)
        {
            missionScreen.CustomCamera = null;
        }

        ladderCamera.ReleaseCamera();
        ladderCamera = null;
        return true;
    }
}
