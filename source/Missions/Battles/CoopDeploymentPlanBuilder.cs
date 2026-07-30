using Common.Logging;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public interface ICoopDeploymentPlanBuilder
{
    void EnsurePlans(Mission mission, DefaultBattleMissionAgentSpawnLogic spawnLogic, BattleSideEnum battleSide);
}

internal class CoopDeploymentPlanBuilder : ICoopDeploymentPlanBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopDeploymentPlanBuilder>();

    public void EnsurePlans(
        Mission mission,
        DefaultBattleMissionAgentSpawnLogic spawnLogic,
        BattleSideEnum battleSide)
    {
        var deploymentPlan = spawnLogic.DeploymentPlan as DefaultMissionDeploymentPlan;
        if (deploymentPlan == null)
            throw new InvalidOperationException("The battle deployment plan was not initialized.");

        foreach (var team in mission.Teams)
        {
            if (team.Side != battleSide || deploymentPlan.IsPlanMade(team)) continue;

            deploymentPlan.MakeDeploymentPlan(team);
            if (!deploymentPlan.IsPlanMade(team))
            {
                throw new InvalidOperationException(
                    $"Failed to create the {battleSide} deployment plan for team {team.TeamIndex}.");
            }

            Logger.Information(
                "[BattleSync] Created missing deployment plan before spawning side {Side} for team {TeamIndex}",
                battleSide,
                team.TeamIndex);
        }
    }
}
