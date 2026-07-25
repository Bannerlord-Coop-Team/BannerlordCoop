using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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
}
