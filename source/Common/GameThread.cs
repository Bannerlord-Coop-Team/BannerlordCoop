using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Common;

public class GameThread : IUpdateable
{
    private static ILogger Logger = LogManager.GetLogger<GameThread>();

    private static readonly Lazy<GameThread> m_Instance =
        new Lazy<GameThread>(() => new GameThread());

    private readonly Queue<(Action Act, EventWaitHandle Wait, string Label, IGameThreadSession Session)> m_Queue =
        new Queue<(Action, EventWaitHandle, string, IGameThreadSession)>();

    private readonly object m_QueueLock = new object();
    private static readonly AsyncLocal<IGameThreadSession> m_AmbientSession =
        new AsyncLocal<IGameThreadSession>();
    private int m_GameLoopThreadId;

    public int QueueLength
    {
        get
        {
            lock (m_QueueLock)
            {
                return m_Queue.Count;
            }
        }
    }

    public bool IsInitialized => m_GameLoopThreadId != 0;

    /// <summary>
    /// True when the caller is running on the game-loop thread that drains the queue in <see cref="Update"/>.
    /// A blocking caller already on this thread must pump <see cref="Update"/> itself while it waits, or it
    /// stalls the very queue its completion depends on.
    /// </summary>
    public bool IsGameThread => Thread.CurrentThread.ManagedThreadId == m_GameLoopThreadId;

    private GameThread()
    {
    }

    public static GameThread Instance => m_Instance.Value;

    #region Instrumentation

    /// <summary>
    /// When true, <see cref="Update"/> times how long it spends draining the queue each frame and
    /// periodically logs a summary: total drain time, action count and rate, the worst single-frame
    /// hitch, the deepest backlog, and the top contributors by cumulative time. This attributes
    /// game-thread (render-thread) lag to the handlers that cause it. Each queued action is labeled
    /// automatically from its caller (file + method) unless an explicit context is supplied, so no
    /// call site needs to change. Off by default; toggle it at runtime on the process you want to
    /// profile (typically the client) with the <c>coop.debug.gamethread.instrument</c> console command.
    /// </summary>
    public static bool Instrument = false;

    /// <summary>How often the drain summary is written to the log.</summary>
    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(1);

    /// <summary>How many of the heaviest labels to list in each summary.</summary>
    private const int TopLabelCount = 10;

    private readonly Stopwatch m_ReportTimer = Stopwatch.StartNew();
    private readonly Dictionary<string, (long Ticks, int Count)> m_PerLabel =
        new Dictionary<string, (long, int)>();
    private int m_WindowFrames;
    private int m_WindowActions;
    private long m_WindowTicks;
    private long m_WorstFrameTicks;
    private int m_WorstFrameActions;
    private int m_WorstBacklog;

    private static double ToMs(long ticks) => 1000.0 * ticks / Stopwatch.Frequency;

    #endregion

    public void Update(TimeSpan frameTime)
    {
        if (Thread.CurrentThread.ManagedThreadId != Instance.m_GameLoopThreadId)
        {
            throw new ArgumentException("Wrong thread!");
        }

        List<(Action Act, EventWaitHandle Wait, string Label, IGameThreadSession Session)> toBeRun =
            new List<(Action, EventWaitHandle, string, IGameThreadSession)>();

        int backlog;
        lock (Instance.m_QueueLock)
        {
            backlog = m_Queue.Count;
            while (m_Queue.Count > 0)
            {
                toBeRun.Add(m_Queue.Dequeue());
            }
        }

        if (!Instrument)
        {
            foreach ((Action Act, EventWaitHandle Wait, string Label, IGameThreadSession Session) task in toBeRun)
            {
                RunQueuedTask(task);
            }
            return;
        }

        long frameStart = Stopwatch.GetTimestamp();
        foreach ((Action Act, EventWaitHandle Wait, string Label, IGameThreadSession Session) task in toBeRun)
        {
            if (task.Session?.IsActive == false)
            {
                task.Wait?.Set();
                continue;
            }

            long actionStart = Stopwatch.GetTimestamp();
            try
            {
                using (ActivateSession(task.Session))
                {
                    task.Act?.Invoke();
                }
            }
            finally
            {
                task.Wait?.Set();
            }
            long actionTicks = Stopwatch.GetTimestamp() - actionStart;

            string label = task.Label ?? "(unlabeled)";
            m_PerLabel.TryGetValue(label, out (long Ticks, int Count) agg);
            m_PerLabel[label] = (agg.Ticks + actionTicks, agg.Count + 1);
        }
        long frameTicks = Stopwatch.GetTimestamp() - frameStart;

        m_WindowFrames++;
        m_WindowActions += toBeRun.Count;
        m_WindowTicks += frameTicks;
        if (frameTicks > m_WorstFrameTicks)
        {
            m_WorstFrameTicks = frameTicks;
            m_WorstFrameActions = toBeRun.Count;
        }
        if (backlog > m_WorstBacklog)
        {
            m_WorstBacklog = backlog;
        }

        if (m_ReportTimer.Elapsed >= ReportInterval)
        {
            ReportAndReset();
        }
    }

    private void ReportAndReset()
    {
        double seconds = m_ReportTimer.Elapsed.TotalSeconds;

        // Skip the noisy log when the game thread did no marshaled work this window.
        if (m_WindowActions > 0)
        {
            string top = string.Join(", ", m_PerLabel
                .OrderByDescending(kv => kv.Value.Ticks)
                .Take(TopLabelCount)
                .Select(kv => $"{kv.Key}={ToMs(kv.Value.Ticks):0.0}ms/{kv.Value.Count}"));

            Logger.Information(
                "[GameThread] {Frames} frames | {Actions} actions ({Rate:0}/s) | drain {Drain:0.0}ms " +
                "({PerFrame:0.00}ms/frame) | worst frame {Worst:0.0}ms/{WorstActions} actions | " +
                "max backlog {Backlog} | top: {Top}",
                m_WindowFrames,
                m_WindowActions,
                m_WindowActions / seconds,
                ToMs(m_WindowTicks),
                ToMs(m_WindowTicks) / Math.Max(1, m_WindowFrames),
                ToMs(m_WorstFrameTicks),
                m_WorstFrameActions,
                m_WorstBacklog,
                top);
        }

        m_PerLabel.Clear();
        m_WindowFrames = 0;
        m_WindowActions = 0;
        m_WindowTicks = 0;
        m_WorstFrameTicks = 0;
        m_WorstFrameActions = 0;
        m_WorstBacklog = 0;
        m_ReportTimer.Restart();
    }

    public int Priority { get; } = UpdatePriority.MainLoop.GameThread;

    /// <summary>
    /// Maximum time a blocking <see cref="Run(Action, bool, string, string, string)"/> call waits for the
    /// game loop to process the queued action before failing. Turns a silent deadlock into a loud error
    /// when the game loop is not pumping (or was never initialized, as in test environments).
    /// </summary>
    public static readonly TimeSpan BlockingTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs a given action on the game thread
    /// </summary>
    /// <param name="action">Action to run on game thread</param>
    /// <param name="blocking">Flag to pause code execution,
    /// True blocks execution until task is complete,
    /// False queues and returns</param>
    /// <param name="label">Optional name used to attribute drain time in the instrumentation summary.
    /// Defaults to the calling file and method, so call sites do not need to pass anything.</param>
    /// <exception cref="TimeoutException">
    /// Thrown for blocking calls when the action was not processed within <see cref="BlockingTimeout"/>.
    /// </exception>
    public static void Run(Action action, bool blocking = false, string label = null,
        [CallerFilePath] string callerFile = null,
        [CallerMemberName] string callerMember = null)
    {
        IGameThreadSession session = m_AmbientSession.Value;
        if (session?.IsActive == false)
        {
            if (blocking)
            {
                throw new OperationCanceledException(
                    $"The game-thread session ended before the blocking {nameof(Run)} action was queued.");
            }
            return;
        }

        if (Thread.CurrentThread.ManagedThreadId == Instance.m_GameLoopThreadId)
        {
            action();
        }
        else
        {
            EventWaitHandle ewh = blocking ?
                new EventWaitHandle(false, EventResetMode.ManualReset) :
                null;

            string resolved = label ?? BuildLabel(callerFile, callerMember);
            lock (Instance.m_QueueLock)
            {
                Instance.m_Queue.Enqueue((action, ewh, resolved, session));
            }

            if (ewh == null) return;

            int waitResult = session == null
                ? (ewh.WaitOne(BlockingTimeout) ? 0 : WaitHandle.WaitTimeout)
                : WaitHandle.WaitAny(
                    new[] { ewh, session.CancellationWaitHandle },
                    BlockingTimeout);
            if (waitResult == WaitHandle.WaitTimeout)
            {
                throw new TimeoutException(
                    $"A blocking {nameof(Run)} action was not processed by the game loop " +
                    $"within {BlockingTimeout.TotalSeconds:0} seconds. The game loop thread is not pumping " +
                    $"{nameof(GameThread)}.{nameof(Update)} (initialized: {Instance.IsInitialized}).");
            }
            if (waitResult == 1)
            {
                throw new OperationCanceledException(
                    $"The game-thread session ended before the blocking {nameof(Run)} action completed.");
            }
        }
    }

    /// <summary>
    /// Runs a given action on the game thread, logging any exception the action throws instead of
    /// letting it propagate. The guard is wrapped around the action itself, so it travels onto the
    /// game thread and catches the failure where the action actually runs (inside <see cref="Update"/>).
    /// This keeps a single failing action from killing the pump and deadlocking blocking callers
    /// waiting on the queue.
    /// </summary>
    /// <param name="action">Action to run on game thread</param>
    /// <param name="blocking">Flag to pause code execution,
    /// True blocks execution until task is complete,
    /// False queues and returns</param>
    /// <param name="context">Optional description of the action, attached to the error log to
    /// identify which caller's action failed, and used to attribute drain time in the instrumentation
    /// summary. Defaults to the calling file and method.</param>
    public static void RunSafe(Action action, bool blocking = false, string context = null,
        [CallerFilePath] string callerFile = null,
        [CallerMemberName] string callerMember = null)
    {
        string label = context ?? BuildLabel(callerFile, callerMember);
        Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to run action on the game thread: {Context}", context ?? "(none)");
            }
        }, blocking, label);
    }

    /// <summary>
    /// Blocks until <paramref name="condition"/> returns true or <paramref name="deadline"/> passes, and
    /// reports which happened, draining <see cref="Update"/> each iteration so the work the condition depends
    /// on — and the blocking <see cref="Run"/> handlers the network thread is waiting on — keeps making
    /// progress; a bare wait on the game-loop thread would stall the very queue it is waiting on, a
    /// self-inflicted deadlock that only breaks at the deadline. Must be called on the game-loop thread,
    /// which owns the pump.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called off the game-loop thread.</exception>
    public static bool WaitWhilePumping(Func<bool> condition, DateTime deadline)
    {
        if (!Instance.IsGameThread)
            throw new InvalidOperationException(
                $"{nameof(WaitWhilePumping)} must be called on the game-loop thread; it drains the queue while it waits.");

        while (true)
        {
            // Drain with the mod's patches live. The queued actions are ordinary game-loop work and must not
            // inherit an AllowedThread allowance the caller happens to hold — that would silence the
            // replication patches the actions rely on. The normal game-loop pump runs them with no allowance,
            // so suspend any ambient one here to match it.
            using (AllowedThread.Suspend())
            {
                // A single failing queued action must not abort the wait (which would also leave that action's
                // own blocking caller waiting out its full timeout); log and keep pumping, mirroring RunSafe.
                // Without this guard the throw would escape into whatever the waiter is doing — e.g. mid
                // battle-start construction.
                try
                {
                    Instance.Update(TimeSpan.Zero);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "A queued action threw while pumping the game thread during a blocking wait");
                }
            }

            if (condition())
                return true;

            if (DateTime.UtcNow >= deadline)
                return false;

            Thread.Sleep(5);
        }
    }

    private static string BuildLabel(string callerFile, string callerMember)
    {
        if (string.IsNullOrEmpty(callerFile))
        {
            return callerMember ?? "(unknown)";
        }
        return $"{Path.GetFileNameWithoutExtension(callerFile)}.{callerMember}";
    }

    public void MarkGameThread()
    {
        m_GameLoopThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    internal static IDisposable ActivateSession(IGameThreadSession session) =>
        new SessionScope(session);

    private static void RunQueuedTask(
        (Action Act, EventWaitHandle Wait, string Label, IGameThreadSession Session) task)
    {
        try
        {
            if (task.Session?.IsActive == false) return;

            using (ActivateSession(task.Session))
            {
                task.Act?.Invoke();
            }
        }
        finally
        {
            task.Wait?.Set();
        }
    }

    private sealed class SessionScope : IDisposable
    {
        private readonly IGameThreadSession previous;

        public SessionScope(IGameThreadSession session)
        {
            previous = m_AmbientSession.Value;
            m_AmbientSession.Value = session;
        }

        public void Dispose()
        {
            m_AmbientSession.Value = previous;
        }
    }
}
