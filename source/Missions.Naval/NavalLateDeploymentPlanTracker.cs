using NavalDLC.Missions.MissionLogics;
using TaleWorlds.Core;

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
}
