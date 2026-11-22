#if DEBUG
using System;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: presses a mission button for the scenario the way a click does: the mission tick only queues the
/// press, which runs after the tick returns.
/// </summary>
public interface INavalScenarioReadyPress
{
    /// <summary>How many times the queued press ran and returned.</summary>
    int Presses { get; }

    /// <summary>
    /// Called from the mission tick: queues one <paramref name="press"/> when <paramref name="canPress"/> holds,
    /// no press is queued and the retry cooldown has passed.
    /// </summary>
    void Tick(float dt, Func<bool> canPress, Action press);
}

/// <inheritdoc cref="INavalScenarioReadyPress"/>
public class NavalScenarioReadyPress : INavalScenarioReadyPress
{
    internal const float RetrySeconds = 3f;

    private readonly Action<Action> enqueueOutsideTick;

    private float cooldown;
    private bool queued;

    public NavalScenarioReadyPress(Action<Action> enqueueOutsideTick)
    {
        this.enqueueOutsideTick = enqueueOutsideTick;
    }

    public int Presses { get; private set; }

    public void Tick(float dt, Func<bool> canPress, Action press)
    {
        cooldown -= dt;
        if (queued || cooldown > 0f || !canPress()) return;

        queued = true;
        cooldown = RetrySeconds;
        enqueueOutsideTick(() =>
        {
            queued = false;
            if (!canPress()) return;

            // A press that throws is no press: only one that returned counts.
            press();
            Presses++;
        });
    }
}
#endif
