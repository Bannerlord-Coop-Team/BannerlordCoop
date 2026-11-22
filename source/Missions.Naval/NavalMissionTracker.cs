using GameInterface;

namespace Missions.Naval;

/// <summary>Tracks whether a coop naval battle runs on this client, so the agent-wide naval patches stay idle elsewhere.</summary>
public interface INavalMissionTracker
{
    /// <summary>[Any thread] True from the naval battle behaviour's start until its end.</summary>
    bool IsActive { get; set; }
}

/// <summary>
/// [Game thread writes] One per client container, which patches resolve; volatile because the engine's async agent tick and
/// fixed tick read it too.
/// </summary>
internal class NavalMissionTracker : INavalMissionTracker
{
    private volatile bool isActive;

    /// <inheritdoc/>
    public bool IsActive
    {
        get => isActive;
        set => isActive = value;
    }

    /// <summary>[Any thread] Whether the client container patches resolve from runs a coop naval battle; false when none resolves.</summary>
    internal static bool IsActiveHere() => ContainerProvider.TryResolve<INavalMissionTracker>(out var tracker) && tracker.IsActive;
}
