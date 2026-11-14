using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Makes a battle side's deployment plan after deployment and tracks while it runs, so the notification patch
/// keeps mission listeners from hearing of it.
/// </summary>
public interface INavalLateDeploymentPlanTracker
{
    /// <summary>Whether the late plan is being made now, which the notification patch reads.</summary>
    bool IsMaking { get; }

    /// <summary>Makes the side's deployment plan with the notification patch silencing it, also when the plan throws.</summary>
    void MakeForSide(DefaultNavalMissionLogic navalLogic, BattleSideEnum side);

    /// <summary>
    /// Plans the allied AI fleet's ships alone at <paramref name="spawnPathOffset"/>, silenced the same way: the ally team also
    /// holds other players' copies, which keep their owners' positions and stay out of the plan.
    /// </summary>
    void MakeForAllyTeam(NavalMissionDeploymentPlanningLogic planning, Team team,
        IEnumerable<(FormationClass Formation, IShipOrigin Ship)> aiShips, float spawnPathOffset, float targetOffset);
}

/// <summary>One per client container, so another client sharing the process keeps announcing its own plans.</summary>
internal class NavalLateDeploymentPlanTracker : INavalLateDeploymentPlanTracker
{
    /// <inheritdoc/>
    public bool IsMaking { get; private set; }

    /// <inheritdoc/>
    public void MakeForSide(DefaultNavalMissionLogic navalLogic, BattleSideEnum side) =>
        Silenced(() => navalLogic.MakeDeploymentPlansForSide(side));

    /// <inheritdoc/>
    public void MakeForAllyTeam(NavalMissionDeploymentPlanningLogic planning, Team team,
        IEnumerable<(FormationClass Formation, IShipOrigin Ship)> aiShips, float spawnPathOffset, float targetOffset) =>
        Silenced(() =>
        {
            // RemakeDeploymentPlan's reset, so a plan made with copies at deployment start can't keep the AI ships out of it.
            planning.ClearAddedShips(team);
            planning.ClearDeploymentPlan(team);
            foreach (var (formation, ship) in aiShips)
                planning.AddShip(team, formation, ship);
            planning.MakeDeploymentPlan(team, spawnPathOffset, targetOffset);
        });

    // Runs the plan with IsMaking set, cleared again also when the plan throws.
    private void Silenced(Action plan)
    {
        IsMaking = true;
        try
        {
            plan();
        }
        finally
        {
            IsMaking = false;
        }
    }
}
