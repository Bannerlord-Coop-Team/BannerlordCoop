using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Common;

public class GameThread : IUpdateable
{
    private static ILogger Logger = LogManager.GetLogger<GameThread>();

    private static readonly Lazy<GameThread> m_Instance =
        new Lazy<GameThread>(() => new GameThread());

    private readonly Queue<(Action, EventWaitHandle)> m_Queue =
        new Queue<(Action, EventWaitHandle)>();

    private readonly object m_QueueLock = new object();
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

    private GameThread()
    {
    }

    public static GameThread Instance => m_Instance.Value;

    public void Update(TimeSpan frameTime)
    {
        if (Thread.CurrentThread.ManagedThreadId != Instance.m_GameLoopThreadId)
        {
            throw new ArgumentException("Wrong thread!");
        }

        List<(Action, EventWaitHandle)> toBeRun = new List<(Action, EventWaitHandle)>();

        lock (Instance.m_QueueLock)
        {
            while (m_Queue.Count > 0)
            {
                toBeRun.Add(m_Queue.Dequeue());
            }
        }
        
        foreach ((Action, EventWaitHandle) task in toBeRun)
        {
            task.Item1?.Invoke();
            task.Item2?.Set();
        }
    }
    public int Priority { get; } = UpdatePriority.MainLoop.GameLoopRunner;

    /// <summary>
    /// Maximum time a blocking <see cref="Run(Action, bool)"/> call waits for the game
    /// loop to process the queued action before failing. Turns a silent deadlock into a loud error
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
    /// <exception cref="TimeoutException">
    /// Thrown for blocking calls when the action was not processed within <see cref="BlockingTimeout"/>.
    /// </exception>
    public static void Run(Action action, bool blocking = false)
    {
        if (Thread.CurrentThread.ManagedThreadId == Instance.m_GameLoopThreadId)
        {
            action();
        }
        else
        {
            EventWaitHandle ewh = blocking ?
                new EventWaitHandle(false, EventResetMode.ManualReset) :
                null;
            lock (Instance.m_QueueLock)
            {
                Instance.m_Queue.Enqueue((action, ewh));
            }

            if (ewh != null && ewh.WaitOne(BlockingTimeout) == false)
            {
                throw new TimeoutException(
                    $"A blocking {nameof(Run)} action was not processed by the game loop " +
                    $"within {BlockingTimeout.TotalSeconds:0} seconds. The game loop thread is not pumping " +
                    $"{nameof(GameThread)}.{nameof(Update)} (initialized: {Instance.IsInitialized}).");
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
    public static void RunSafe(Action action, bool blocking = false)
    {
        Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to run action on the game thread");
            }
        }, blocking);
    }

    public void SetGameThreadThread()
    {
        m_GameLoopThreadId = Thread.CurrentThread.ManagedThreadId;
    }
}
