using GameInterface;
using Missions.Agents.Packets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Battles;

/// <summary>Reports state needed to verify co-op battle synchronization.</summary>
internal static class BattleDebugCommands
{
    private static readonly Dictionary<int, Vec3> EnemyPositions = new Dictionary<int, Vec3>();
    private static Mission observedMission;

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

        return $"instance={controller.Session.InstanceId} host={controller.Session.IsLocalHost} " +
            $"activated={controller.Deployment.IsActivated} committed={controller.Deployment.IsCommitted} " +
            $"deploymentReady={deploymentReady} mainAgent={Agent.Main != null} activeAgents={activeAgents} " +
            $"playerSide={playerTeam?.Side.ToString() ?? "None"} enemyParties={enemyParties} enemyActive={enemies.Count} " +
            $"enemyAi={enemies.Count(agent => agent.IsAIControlled)} enemyMovedSinceLast={moved}";
    }

    [CommandLineArgumentFunction("mount_state", "coop.debug.battle")]
    public static string MountState(List<string> args)
    {
        if (args.Count > 1)
            return "Usage: coop.debug.battle.mount_state [host|host-player-team|local|controllerId]";

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        string filter = args.Count == 1 ? args[0] : null;
        var mounts = registry.GetControllerIds()
            .SelectMany(registry.GetAgents)
            .Where(info => MatchesAuthority(
                controller.Session,
                info,
                filter,
                mission.PlayerTeam))
            .Where(info => info.Agent != null && info.Agent.IsMount && info.Agent.IsActive())
            .OrderBy(info => info.AgentId)
            .ToArray();

        int stationaryCount = 0;
        int stationaryAnimatedCount = 0;
        int stationaryTurningCount = 0;
        var output = new StringBuilder();
        foreach (var info in mounts)
        {
            Agent mount = info.Agent;
            float speed = mount.GetRealGlobalVelocity().AsVec2.Length;
            bool stationary = speed <= AgentMountData.StationarySpeedThreshold;
            var skeleton = mount.AgentVisuals?.GetSkeleton();
            string animationName = skeleton?.GetAnimationAtChannel(0);
            float animationSpeed = skeleton?.GetAnimationSpeedAtChannel(0) ?? 0f;
            int actionIndex = mount.GetCurrentAction(0).Index;
            string actionName = AgentActionData.GetActionNameWithCode(actionIndex);
            int turnDirection = AgentMountData.GetTurnDirection(actionName, animationName);
            bool locomotionAction = AgentMountData.IsLocomotionAction(actionIndex, animationName);
            bool stationaryAnimated = stationary
                && locomotionAction
                && animationSpeed > 0.001f;
            bool stationaryTurning = stationary
                && turnDirection != AgentMountData.NoTurn
                && animationSpeed > 0.001f;
            if (stationary) stationaryCount++;
            if (stationaryAnimated) stationaryAnimatedCount++;
            if (stationaryTurning) stationaryTurningCount++;

            string riderId = "none";
            if (mount.RiderAgent != null
                && registry.TryGetAgentInfo(mount.RiderAgent, out var riderInfo))
            {
                riderId = riderInfo.AgentId.ToString("N");
            }

            output.Append("id=").Append(info.AgentId.ToString("N"))
                .Append(" authority=").Append(info.CurrentAuthority)
                .Append(" local=").Append(controller.Session.IsOwn(info.CurrentAuthority))
                .Append(" rider=").Append(riderId)
                .Append(" position=").Append(mount.Position.X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',').Append(mount.Position.Y.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" speed=").Append(speed.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" input=").Append(mount.MovementInputVector.X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',').Append(mount.MovementInputVector.Y.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" direction=").Append(mount.GetMovementDirection().X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',').Append(mount.GetMovementDirection().Y.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" action0=").Append(actionIndex)
                .Append(" actionName=").Append(actionName ?? "none")
                .Append(" actionProgress=").Append(mount.GetCurrentActionProgress(0).ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" animation=").Append(animationName ?? "none")
                .Append(" animationSpeed=").Append(animationSpeed.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" locomotion=").Append(locomotionAction)
                .Append(" turnDirection=").Append(turnDirection)
                .Append(" stationaryTurning=").Append(stationaryTurning)
                .Append(" stationaryAnimated=").Append(stationaryAnimated)
                .AppendLine();
        }

        output.Insert(
            0,
            $"mounts={mounts.Length} stationary={stationaryCount} stationaryAnimated={stationaryAnimatedCount} " +
            $"stationaryTurning={stationaryTurningCount} " +
            $"own={controller.Session.OwnControllerId} host={controller.Session.IsLocalHost}{Environment.NewLine}");
        return output.ToString().TrimEnd();
    }

    [CommandLineArgumentFunction("move_cavalry", "coop.debug.battle")]
    public static string MoveCavalry(List<string> args)
    {
        if (args.Count != 1
            || !float.TryParse(
                args[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float distance)
            || distance < 5f
            || distance > 100f)
        {
            return "Usage: coop.debug.battle.move_cavalry <distance: 5-100>";
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!controller.Session.IsLocalHost)
            return "Run this command on the battle-host client";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        Agent[] riders = GetBattleHostCavalryRiders(
            mission,
            controller,
            registry);
        Formation[] formations = riders
            .Select(agent => agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The battle host has no active cavalry formations";

        foreach (Agent rider in riders)
        {
            rider.SetIsAIPaused(false);
            rider.MountAgent?.SetIsAIPaused(false);
        }

        foreach (Formation formation in formations)
        {
            Vec2 direction = formation.Direction;
            if (direction.LengthSquared <= 0.0001f)
                direction = Vec2.Forward;
            else
                direction.Normalize();

            WorldPosition destination = formation.CachedMedianPosition;
            destination.SetVec2(formation.CurrentPosition + (direction * distance));
            formation.SetMovementOrder(MovementOrder.MovementOrderMove(destination));
        }

        return $"Moved {formations.Length} battle-host cavalry formations {distance:0.0} meters";
    }

    [CommandLineArgumentFunction("hold_cavalry", "coop.debug.battle")]
    public static string HoldCavalry(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.hold_cavalry";

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!controller.Session.IsLocalHost)
            return "Run this command on the battle-host client";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        Agent[] riders = GetBattleHostCavalryRiders(
            mission,
            controller,
            registry);
        Formation[] formations = riders
            .Select(agent => agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The battle host has no active cavalry formations";

        foreach (Formation formation in formations)
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
        foreach (Agent rider in riders)
        {
            rider.MovementInputVector = Vec2.Zero;
            rider.SetIsAIPaused(true);
            if (rider.MountAgent != null)
            {
                rider.MountAgent.MovementInputVector = Vec2.Zero;
                rider.MountAgent.SetIsAIPaused(true);
            }
        }

        return $"Stopped {formations.Length} battle-host cavalry formations";
    }

    [CommandLineArgumentFunction("turn_cavalry", "coop.debug.battle")]
    public static string TurnCavalry(List<string> args)
    {
        if (args.Count != 1
            || !float.TryParse(
                args[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float degrees)
            || float.IsNaN(degrees)
            || float.IsInfinity(degrees)
            || Math.Abs(degrees) < 15f
            || Math.Abs(degrees) > 180f)
        {
            return "Usage: coop.debug.battle.turn_cavalry <degrees: -180 to -15 or 15 to 180>";
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!controller.Session.IsLocalHost)
            return "Run this command on the battle-host client";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        Agent[] riders = GetBattleHostCavalryRiders(
            mission,
            controller,
            registry);
        Formation[] formations = riders
            .Select(agent => agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The battle host has no active cavalry formations";

        foreach (Agent rider in riders)
        {
            rider.SetIsAIPaused(false);
            rider.MountAgent?.SetIsAIPaused(false);
        }

        float radians = degrees * ((float)Math.PI / 180f);
        float cosine = (float)Math.Cos(radians);
        float sine = (float)Math.Sin(radians);
        foreach (Formation formation in formations)
        {
            Vec2 direction = formation.Direction;
            if (direction.LengthSquared <= 0.0001f)
                direction = Vec2.Forward;
            else
                direction.Normalize();

            var turnedDirection = new Vec2(
                (direction.X * cosine) - (direction.Y * sine),
                (direction.X * sine) + (direction.Y * cosine));
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
            formation.SetFacingOrder(
                FacingOrder.FacingOrderLookAtDirection(turnedDirection));
        }

        return $"Turned {formations.Length} battle-host cavalry formations {degrees:0.0} degrees in place";
    }

    private static Agent[] GetBattleHostCavalryRiders(
        Mission mission,
        CoopBattleController controller,
        INetworkAgentRegistry registry)
    {
        return registry.GetAgents(controller.Session.OwnControllerId)
            .Where(info => info.OriginalOwner == controller.Session.OwnControllerId)
            .Select(info => info.Agent)
            .Where(agent => agent != null
                && agent.IsActive()
                && !agent.IsMount
                && agent.HasMount
                && agent.Team == mission.PlayerTeam
                && agent.Formation != null)
            .ToArray();
    }

    private static bool MatchesAuthority(
        IBattleSession session,
        CoopAgentInfo info,
        string filter,
        Team playerTeam)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        if (filter == "host") return session.IsHostController(info.CurrentAuthority);
        if (filter == "host-player-team")
            return session.IsHostController(info.CurrentAuthority)
                && info.OriginalOwner == info.CurrentAuthority
                && info.Agent?.RiderAgent?.IsActive() == true
                && info.Agent.RiderAgent.Team?.Side == playerTeam?.Side;
        if (filter == "local") return session.IsOwn(info.CurrentAuthority);
        return info.CurrentAuthority == filter;
    }
}
