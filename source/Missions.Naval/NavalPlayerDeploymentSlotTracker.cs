using NavalDLC.Missions.MissionLogics;
using System;

namespace Missions.Naval;

/// <summary>
/// [Game thread] This client's own fleet slot among its side's player parties, and the side whose initial plan is being made.
/// The deployment planning patches resolve it.
/// </summary>
public interface INavalPlayerDeploymentSlotTracker
{
    /// <summary>
    /// The own party's index among the side's player parties; 0 is the slot at vanilla's base, or the first room from the path
    /// start.
    /// </summary>
    int Rank { get; set; }

    /// <summary>
    /// How many player parties fight on the own side, the own one included, taken once when this client's mission opens and read
    /// for this client's own slot only; one or none keeps vanilla's plan.
    /// </summary>
    int PlayerCount { get; set; }

    /// <summary>The naval logic whose initial side plan is being made, so the plan patch can read its team range.</summary>
    DefaultNavalMissionLogic PlanningSide { get; }

    /// <summary>Marks the side's initial plan as being made until the returned scope is disposed.</summary>
    IDisposable PlanSide(DefaultNavalMissionLogic side);

    /// <summary>Clears the per-mission slot state.</summary>
    void Reset();
}

/// <summary>One per client container, so E2E clients sharing a process each keep their own slot.</summary>
internal class NavalPlayerDeploymentSlotTracker : INavalPlayerDeploymentSlotTracker
{
    /// <inheritdoc/>
    public int Rank { get; set; }

    /// <inheritdoc/>
    public int PlayerCount { get; set; }

    /// <inheritdoc/>
    public DefaultNavalMissionLogic PlanningSide { get; private set; }

    /// <inheritdoc/>
    public IDisposable PlanSide(DefaultNavalMissionLogic side)
    {
        var scope = new PlanningScope(this, PlanningSide);
        PlanningSide = side;
        return scope;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        Rank = 0;
        PlayerCount = 0;
        PlanningSide = null;
    }

    /// <summary>Restores the side that was planning before the scope opened.</summary>
    private sealed class PlanningScope : IDisposable
    {
        private readonly NavalPlayerDeploymentSlotTracker tracker;
        private readonly DefaultNavalMissionLogic previous;

        // Remembers the side the scope replaced.
        public PlanningScope(NavalPlayerDeploymentSlotTracker tracker, DefaultNavalMissionLogic previous)
        {
            this.tracker = tracker;
            this.previous = previous;
        }

        // Puts the previous planning side back.
        public void Dispose() => tracker.PlanningSide = previous;
    }
}
