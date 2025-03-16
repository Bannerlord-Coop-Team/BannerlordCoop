using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Common;

public class GameLoopRunner : IUpdateable
{
    private static ILogger Logger = LogManager.GetLogger<GameLoopRunner>();

    private static readonly Lazy<GameLoopRunner> m_Instance =
        new Lazy<GameLoopRunner>(() => new GameLoopRunner());

    private readonly Queue<(Action, EventWaitHandle)> m_Queue =
        new Queue<(Action, EventWaitHandle)>();

    private readonly object m_QueueLock = new object();
    private int m_GameLoopThreadId;

    public int QueueLength => m_Queue.Count;

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
    /// Runs a given action on the game thread
    /// </summary>
    /// <param name="action">Action to run on game thread</param>
    /// <param name="blocking">Flag to pause code execution,
    /// True blocks execution until task is complete,
    /// False queues and returns</param>
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
            lock (Instance.m_QueueLock)
            {
                Instance.m_Queue.Enqueue((action, ewh));
            }

            ewh?.WaitOne();
        }
    }

    public void SetGameLoopThread()
    {
        m_GameLoopThreadId = Thread.CurrentThread.ManagedThreadId;
    }
}
