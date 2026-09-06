using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Common;

public class GameThread : IUpdateable
{
    private static ILogger Logger = LogManager.GetLogger<GameThread>();

    private static readonly Lazy<GameThread> m_Instance =
        new Lazy<GameThread>(() => new GameThread());

    internal sealed class QueueContext
    {
        internal readonly Queue<QueuedAction> queue = new Queue<QueuedAction>();
        internal readonly object gate = new object();
        internal bool isClosed;
        internal int rejectedAfterCloseCount;
        private Action waitPump;

        internal int Count
        {
            get
            {
                lock (gate)
                {
                    return queue.Count;
                }
            }
        }

        internal int RejectedAfterCloseCount
        {
            get
            {
                lock (gate)
                {
                    return rejectedAfterCloseCount;
                }
            }
        }

        internal Action WaitPump
        {
            get
            {
                lock (gate)
                {
                    return isClosed ? null : waitPump;
                }
            }
            set
            {
                lock (gate)
                {
                    if (isClosed && value != null)
                        throw new InvalidOperationException("Cannot configure a closed game-thread queue.");
                    waitPump = value;
                }
            }
        }
    }

    internal sealed class QueuedAction
    {
        private readonly object completionGate = new object();
        private QueuedActionCompletionState completionState;
        private Exception failure;
        private string cancellationReason;

        internal Action Act { get; }
        internal EventWaitHandle Wait { get; }
        internal string Label { get; }
        internal CancellationToken Cancellation { get; }

        internal QueuedAction(
            Action act,
            EventWaitHandle wait,
            string label,
            CancellationToken cancellation)
        {
            Act = act;
            Wait = wait;
            Label = label;
            Cancellation = cancellation;
        }

        internal void CompleteExecuted() => Complete(QueuedActionCompletionState.Executed, null, null);

        internal void CompleteCanceled(string reason) =>
            Complete(QueuedActionCompletionState.Canceled, null, reason);

        internal void CompleteFailed(Exception exception) =>
            Complete(QueuedActionCompletionState.Failed, exception, null);

        internal void ThrowIfNotExecuted()
        {
            QueuedActionCompletionState state;
            Exception completedFailure;
            string completedCancellationReason;
            lock (completionGate)
            {
                state = completionState;
                completedFailure = failure;
                completedCancellationReason = cancellationReason;
            }

            switch (state)
            {
                case QueuedActionCompletionState.Executed:
                    return;
                case QueuedActionCompletionState.Canceled:
                    throw new OperationCanceledException(
                        completedCancellationReason ?? "The queued game-thread action was canceled.");
                case QueuedActionCompletionState.Failed:
                    ExceptionDispatchInfo.Capture(completedFailure).Throw();
                    return;
                default:
                    throw new InvalidOperationException(
                        "The queued game-thread action was signaled without a completion state.");
            }
        }

        private void Complete(
            QueuedActionCompletionState state,
            Exception completedFailure,
            string completedCancellationReason)
        {
            lock (completionGate)
            {
                if (completionState != QueuedActionCompletionState.Pending)
                    return;

                failure = completedFailure;
                cancellationReason = completedCancellationReason;
                completionState = state;
                Wait?.Set();
            }
        }
    }

    private enum QueuedActionCompletionState
    {
        Pending,
        Executed,
        Canceled,
        Failed,
    }

    private readonly QueueContext m_DefaultQueue = new QueueContext();
    private static readonly AsyncLocal<QueueContext> m_AmbientQueue =
        new AsyncLocal<QueueContext>();
    private static readonly AsyncLocal<CancellationToken> m_AmbientCancellation =
        new AsyncLocal<CancellationToken>();
    private int m_GameLoopThreadId;

    public int QueueLength
    {
        get
        {
            return CurrentQueue.Count;
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

    private QueueContext CurrentQueue => m_AmbientQueue.Value ?? m_DefaultQueue;

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

        List<QueuedAction> toBeRun = new List<QueuedAction>();

        int backlog;
        QueueContext queueContext = Instance.CurrentQueue;
        lock (queueContext.gate)
        {
            backlog = queueContext.queue.Count;
            while (queueContext.queue.Count > 0)
            {
                toBeRun.Add(queueContext.queue.Dequeue());
            }
        }

        if (!Instrument)
        {
            for (int index = 0; index < toBeRun.Count; index++)
            {
                try
                {
                    RunQueuedTask(toBeRun[index]);
                }
                catch
                {
                    CancelUnrunBatch(toBeRun, index + 1);
                    throw;
                }
            }
            return;
        }

        long frameStart = Stopwatch.GetTimestamp();
        for (int index = 0; index < toBeRun.Count; index++)
        {
            QueuedAction task = toBeRun[index];
            if (task.Cancellation.IsCancellationRequested)
            {
                task.CompleteCanceled("The game-thread session ended before the queued action ran.");
                continue;
            }

            long actionStart = Stopwatch.GetTimestamp();
            try
            {
                using (ActivateCancellation(task.Cancellation))
                {
                    task.Act?.Invoke();
                }
                task.CompleteExecuted();
            }
            catch (Exception e)
            {
                task.CompleteFailed(e);
                CancelUnrunBatch(toBeRun, index + 1);
                throw;
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
        CancellationToken cancellation = m_AmbientCancellation.Value;
        if (cancellation.IsCancellationRequested)
        {
            if (blocking)
            {
                throw new OperationCanceledException(
                    $"The game-thread session ended before the blocking {nameof(Run)} action was queued.");
            }
            // This return is one of the few places marshalled work can vanish without a trace;
            // name the dropped action so a lost state-apply is diagnosable from the log.
            Logger.Warning("Dropping game-thread action {Label}: the session was cancelled before it was queued",
                label ?? BuildLabel(callerFile, callerMember));
            return;
        }

        string resolved = label ?? BuildLabel(callerFile, callerMember);
        QueueContext queueContext = Instance.CurrentQueue;
        if (Thread.CurrentThread.ManagedThreadId == Instance.m_GameLoopThreadId)
        {
            bool rejected;
            lock (queueContext.gate)
            {
                rejected = queueContext.isClosed;
                if (rejected)
                    queueContext.rejectedAfterCloseCount++;
            }

            if (rejected)
            {
                RejectClosedQueueAction(blocking, resolved);
                return;
            }

            action();
        }
        else
        {
            EventWaitHandle ewh = blocking ?
                new EventWaitHandle(false, EventResetMode.ManualReset) :
                null;
            var queuedAction = new QueuedAction(action, ewh, resolved, cancellation);

            bool rejected;
            lock (queueContext.gate)
            {
                rejected = queueContext.isClosed;
                if (rejected)
                    queueContext.rejectedAfterCloseCount++;
                else
                    queueContext.queue.Enqueue(queuedAction);
            }

            if (rejected)
            {
                ewh?.Dispose();
                RejectClosedQueueAction(blocking, resolved);
                return;
            }

            if (ewh == null) return;

            int waitResult = !cancellation.CanBeCanceled
                ? (ewh.WaitOne(BlockingTimeout) ? 0 : WaitHandle.WaitTimeout)
                : WaitHandle.WaitAny(
                    new[] { ewh, cancellation.WaitHandle },
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

            queuedAction.ThrowIfNotExecuted();
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
        Run(WrapSafe(action, context), blocking, label);
    }

    /// <summary>
    /// Queues an action for a later <see cref="Update"/> even when called from the game-loop thread.
    /// Use this when running inline would mutate state currently being iterated by the engine.
    /// </summary>
    public static void EnqueueSafe(Action action, string context = null,
        [CallerFilePath] string callerFile = null,
        [CallerMemberName] string callerMember = null)
    {
        CancellationToken cancellation = m_AmbientCancellation.Value;
        if (cancellation.IsCancellationRequested) return;

        string label = context ?? BuildLabel(callerFile, callerMember);
        QueueContext queueContext = Instance.CurrentQueue;
        bool rejected;
        lock (queueContext.gate)
        {
            rejected = queueContext.isClosed;
            if (rejected)
                queueContext.rejectedAfterCloseCount++;
            else
                queueContext.queue.Enqueue(new QueuedAction(
                    WrapSafe(action, context),
                    null,
                    label,
                    cancellation));
        }

        if (rejected)
            RejectClosedQueueAction(false, label);
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
            // Production's network thread keeps flushing while the game loop waits. Isolated runtimes
            // can provide the equivalent tick on their queue without changing the production default.
            Instance.CurrentQueue.WaitPump?.Invoke();

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

    private static Action WrapSafe(Action action, string context) => () =>
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to run action on the game thread: {Context}", context ?? "(none)");
        }
    };

    private static void RejectClosedQueueAction(bool blocking, string label)
    {
        if (blocking)
        {
            throw new OperationCanceledException(
                $"The game-thread queue was closed before blocking action {label} could run.");
        }

        Logger.Warning(
            "Dropping game-thread action {Label}: its isolated runtime queue is closed",
            label);
    }

    public void MarkGameThread()
    {
        m_GameLoopThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// The currently registered game-loop thread id (0 when unmarked). Pair with
    /// <see cref="RestoreGameThread"/> so a scope that re-marks the game thread (e.g. a test harness
    /// running a call on a worker thread) can put the previous registration back instead of leaving
    /// the mark on a thread that may never pump the queue again.
    /// </summary>
    public int GameThreadId => m_GameLoopThreadId;

    /// <summary>
    /// Restores a registration previously read from <see cref="GameThreadId"/>.
    /// </summary>
    public void RestoreGameThread(int threadId)
    {
        m_GameLoopThreadId = threadId;
    }

    /// <summary>
    /// Discards every queued action without running it, releasing any blocked callers waiting on
    /// them. For test harnesses at environment boundaries: an action queued by a previous test
    /// would otherwise execute inside a later environment's pump against a torn-down container.
    /// </summary>
    public void DiscardQueuedActions()
    {
        DiscardQueuedActions(CurrentQueue, close: false);
    }

    /// <summary>
    /// Discards one isolated runtime's queue without relying on the caller's ambient queue scope.
    /// </summary>
    internal int DiscardQueuedActions(QueueContext queueContext)
    {
        return DiscardQueuedActions(queueContext, close: false);
    }

    /// <summary>
    /// Atomically closes one isolated runtime queue and releases everything waiting in it.
    /// </summary>
    internal int CloseAndDiscardQueuedActions(QueueContext queueContext)
    {
        return DiscardQueuedActions(queueContext, close: true);
    }

    private int DiscardQueuedActions(QueueContext queueContext, bool close)
    {
        if (queueContext == null) throw new ArgumentNullException(nameof(queueContext));

        List<QueuedAction> discarded;
        lock (queueContext.gate)
        {
            if (close)
            {
                queueContext.isClosed = true;
                queueContext.WaitPump = null;
            }
            discarded = new List<QueuedAction>(queueContext.queue);
            queueContext.queue.Clear();
        }

        if (discarded.Count > 0)
        {
            // A non-empty queue here means marshalled work was enqueued but never pumped — for a
            // test harness that is a silently lost state-apply, so name every dropped action.
            Logger.Warning("Discarding {Count} queued game-thread action(s) that no pump ever ran: {Labels}",
                discarded.Count,
                string.Join(", ", discarded.Select(task => task.Label ?? "(unlabeled)")));
        }

        foreach (var task in discarded)
        {
            task.CompleteCanceled("The isolated runtime queue closed before the action ran.");
        }

        return discarded.Count;
    }

    /// <summary>
    /// Clears the game-loop thread registration. A thread that was marked via
    /// <see cref="MarkGameThread"/> must call this before it exits: .NET recycles managed thread
    /// ids, so a registration left behind by a dead thread can silently promote an unrelated
    /// future thread to "game thread", flipping <see cref="Run(Action, bool, string, string, string)"/>
    /// from queueing to inline execution.
    /// </summary>
    public void UnmarkGameThread()
    {
        m_GameLoopThreadId = 0;
    }

    public static IDisposable ActivateCancellation(CancellationToken cancellation) =>
        new CancellationScope(cancellation);

    /// <summary>
    /// Selects the queue owned by one isolated runtime while the scope is active. Production uses the
    /// default process queue; the in-process E2E harness assigns one context per simulated process.
    /// </summary>
    internal static IDisposable ActivateQueue(QueueContext queueContext)
    {
        if (queueContext == null) throw new ArgumentNullException(nameof(queueContext));
        return new QueueScope(queueContext);
    }

    private static void RunQueuedTask(QueuedAction task)
    {
        try
        {
            if (task.Cancellation.IsCancellationRequested)
            {
                task.CompleteCanceled("The game-thread session ended before the queued action ran.");
                return;
            }

            using (ActivateCancellation(task.Cancellation))
            {
                task.Act?.Invoke();
            }
            task.CompleteExecuted();
        }
        catch (Exception e)
        {
            task.CompleteFailed(e);
            throw;
        }
    }

    private static void CancelUnrunBatch(IReadOnlyList<QueuedAction> batch, int firstUnrunIndex)
    {
        int unrunCount = batch.Count - firstUnrunIndex;
        if (unrunCount <= 0) return;

        Logger.Warning(
            "Canceling {Count} dequeued game-thread action(s) after an earlier action failed: {Labels}",
            unrunCount,
            string.Join(", ", batch.Skip(firstUnrunIndex).Select(task => task.Label ?? "(unlabeled)")));

        for (int index = firstUnrunIndex; index < batch.Count; index++)
        {
            batch[index].CompleteCanceled(
                "An earlier game-thread action failed before this queued action could run.");
        }
    }

    private sealed class CancellationScope : IDisposable
    {
        private readonly CancellationToken previous;

        public CancellationScope(CancellationToken cancellation)
        {
            previous = m_AmbientCancellation.Value;
            m_AmbientCancellation.Value = cancellation;
        }

        public void Dispose()
        {
            m_AmbientCancellation.Value = previous;
        }
    }

    private sealed class QueueScope : IDisposable
    {
        private readonly QueueContext previous;

        public QueueScope(QueueContext queueContext)
        {
            previous = m_AmbientQueue.Value;
            m_AmbientQueue.Value = queueContext;
        }

        public void Dispose()
        {
            m_AmbientQueue.Value = previous;
        }
    }
}
