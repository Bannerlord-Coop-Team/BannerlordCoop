using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
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
    /// Plans the allied AI fleet alone in its slot ahead of the side's player fleets, silenced the same way: the ally team also
    /// holds other players' copies, which keep their owners' positions and stay out of the plan. Returns the slot it used.
    /// </summary>
    NavalSideSlot MakeForAllyTeam(NavalMissionDeploymentPlanningLogic planning, Team team,
        IEnumerable<(FormationClass Formation, IShipOrigin Ship)> aiShips);
}

/// <summary>One per client container, so another client sharing the process keeps announcing its own plans.</summary>
internal class NavalLateDeploymentPlanTracker : INavalLateDeploymentPlanTracker
{
    /// <inheritdoc/>
    public bool IsMaking { get; private set; }

    /// <inheritdoc/>
    public void MakeForSide(DefaultNavalMissionLogic navalLogic, BattleSideEnum side)
    {
        IsMaking = true;
        try
        {
            navalLogic.MakeDeploymentPlansForSide(side);
        }
        finally
        {
            IsMaking = false;
        }
    }

    /// <inheritdoc/>
    public NavalSideSlot MakeForAllyTeam(NavalMissionDeploymentPlanningLogic planning, Team team,
        IEnumerable<(FormationClass Formation, IShipOrigin Ship)> aiShips)
    {
        var slot = NavalPlayerDeploymentSlot.AllySlotFor(Mission.Current.GetInitialSpawnPathData(team.Side),
            NavalPlayerDeploymentSlot.LongestTeamRangeOfGame());
        IsMaking = true;
        try
        {
            // RemakeDeploymentPlan's reset, so a plan made with copies at deployment start can't keep the AI ships out of it.
            planning.ClearAddedShips(team);
            planning.ClearDeploymentPlan(team);
            foreach (var (formation, ship) in aiShips)
                planning.AddShip(team, formation, ship);
            planning.MakeDeploymentPlan(team, slot.Offset, slot.TargetOffset);
        }
        finally
        {
            IsMaking = false;
        }

        return slot;
    }
}
