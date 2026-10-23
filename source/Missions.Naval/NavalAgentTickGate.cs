using Common;
using Common.Logging;
using Serilog;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Waits for the engine's async agent and team tick to finish before hull state it reads changes. That tick runs
/// on a worker from the end of Mission.OnTick until the next frame's OnPreTick, which overlaps the game-thread queue's drain.
/// </summary>
public interface INavalAgentTickGate
{
    /// <summary>Blocks until the frame's async agent and team tick finished; false when it timed out and the caller goes on anyway.</summary>
    bool WaitForAgentTick(string reason);
}

/// <inheritdoc cref="INavalAgentTickGate"/>
public class NavalAgentTickGate : INavalAgentTickGate
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalAgentTickGate>();

    // Well above an agent tick; vanilla's own OnPreTick waits without a limit.
    internal const int TimeoutMilliseconds = 2000;

    // Only the game thread waits: the worker reaching here would wait on its own tick.
    public bool WaitForAgentTick(string reason)
    {
        var mission = Mission.Current;
        if (mission == null || mission.tickCompleted || !GameThread.Instance.IsGameThread) return true;

        var clock = Stopwatch.StartNew();
        if (!SpinWait.SpinUntil(() => mission.tickCompleted, TimeoutMilliseconds))
        {
            Logger.Error("[NavalSync] The async agent tick didn't finish within {Milliseconds} ms before {Reason}; going on anyway",
                TimeoutMilliseconds, reason);
            return false;
        }

        Logger.Debug("[NavalSync] Waited {Milliseconds:0.00} ms for the async agent tick before {Reason}", clock.Elapsed.TotalMilliseconds,
            reason);
        return true;
    }
}
