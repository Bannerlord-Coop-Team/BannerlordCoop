using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Common;

public class GameLoopRunner : IUpdateable
{
    private static ILogger Logger = LogManager.GetLogger<GameLoopRunner>();

    private static readonly Lazy<GameLoopRunner> m_Instance =
        new Lazy<GameLoopRunner>(() => new GameLoopRunner());

    private readonly Queue<(Action Action, EventWaitHandle WaitHandle, StrongBox<ExceptionDispatchInfo> Error)> m_Queue =
        new Queue<(Action, EventWaitHandle, StrongBox<ExceptionDispatchInfo>)>();

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

    private GameLoopRunner()
    {
    }

    public static GameLoopRunner Instance => m_Instance.Value;

    public void Update(TimeSpan frameTime)
    {
        if (Thread.CurrentThread.ManagedThreadId != Instance.m_GameLoopThreadId)
        {
            throw new ArgumentException("Wrong thread!");
        }

        var toBeRun = new List<(Action Action, EventWaitHandle WaitHandle, StrongBox<ExceptionDispatchInfo> Error)>();

        lock (Instance.m_QueueLock)
        {
            while (m_Queue.Count > 0)
            {
                toBeRun.Add(m_Queue.Dequeue());
            }
        }

        foreach (var task in toBeRun)
        {
            try
            {
                task.Action?.Invoke();
            }
            catch (Exception ex)
            {
                // Guard each queued action so one that throws cannot abort the drain of the rest of
                // the frame's actions. For a blocking caller, hand the failure back so it rethrows
                // instead of resuming as if the work succeeded; a fire-and-forget action is logged.
                if (task.Error != null)
                {
                    task.Error.Value = ExceptionDispatchInfo.Capture(ex);
                }
                else
                {
                    Logger.Error(ex, "A queued game-loop action threw");
                }
            }
            finally
            {
                // Always signal the waiter so a blocking caller is not left waiting out the full timeout.
                task.WaitHandle?.Set();
            }
        }
    }
    public int Priority { get; } = UpdatePriority.MainLoop.GameLoopRunner;

    /// <summary>
    /// Maximum time a blocking <see cref="RunOnMainThread(Action, bool)"/> call waits for the game
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
    public static void RunOnMainThread(Action action, bool blocking = false)
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
            StrongBox<ExceptionDispatchInfo> error = blocking ?
                new StrongBox<ExceptionDispatchInfo>() :
                null;
            lock (Instance.m_QueueLock)
            {
                Instance.m_Queue.Enqueue((action, ewh, error));
            }

            if (ewh != null)
            {
                if (ewh.WaitOne(BlockingTimeout) == false)
                {
                    throw new TimeoutException(
                        $"A blocking {nameof(RunOnMainThread)} action was not processed by the game loop " +
                        $"within {BlockingTimeout.TotalSeconds:0} seconds. The game loop thread is not pumping " +
                        $"{nameof(GameLoopRunner)}.{nameof(Update)} (initialized: {Instance.IsInitialized}).");
                }

                // If the action threw on the game loop, rethrow it here on the calling thread so a
                // blocking caller sees the failure instead of resuming as if the work succeeded.
                error.Value?.Throw();
            }
        }
    }

    public void SetGameLoopThread()
    {
        m_GameLoopThreadId = Thread.CurrentThread.ManagedThreadId;
    }
}
