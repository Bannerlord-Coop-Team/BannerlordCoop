#if DEBUG
using System;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: presses the naval order of battle's Ready for the scenario the way a click does, outside
/// Mission.OnTick: the mission tick only queues the press, which runs after the tick returns and only if Ready
/// still accepts then.
/// </summary>
public interface INavalScenarioReadyPress
{
    /// <summary>How many times the queued press actually ran.</summary>
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
        if (enqueueOutsideTick == null) throw new ArgumentNullException(nameof(enqueueOutsideTick));

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

            Presses++;
            press();
        });
    }
}
#endif
