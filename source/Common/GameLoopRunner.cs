using System;
using System.Collections.Generic;
using System.Threading;

namespace Common
{
    public class GameLoopRunner : IUpdateable
    {
        private static readonly Lazy<GameLoopRunner> m_Instance =
            new Lazy<GameLoopRunner>(() => new GameLoopRunner());

        private readonly List<(Action, EventWaitHandle)> m_Queue =
            new List<(Action, EventWaitHandle)>();

        private readonly object m_QueueLock = new object();
        private int m_GameLoopThreadId;

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
            lock (m_Queue)
            {
                toBeRun.AddRange(m_Queue);
                m_Queue.Clear();
            }

            foreach ((Action, EventWaitHandle) task in toBeRun)
            {
                task.Item1?.Invoke();
                task.Item2?.Set();
            }
        }
        public int Priority { get; } = UpdatePriority.MainLoop.GameLoopRunner;

        public static void RunOnMainThread(Action action, bool bBlocking = false)
        {
            if (Thread.CurrentThread.ManagedThreadId == Instance.m_GameLoopThreadId)
            {
                action();
            }
            else
            {
                EventWaitHandle ewh = bBlocking ?
                    new EventWaitHandle(false, EventResetMode.ManualReset) :
                    null;
                lock (Instance.m_QueueLock)
                {
                    Instance.m_Queue.Add((action, ewh));
                }

                ewh?.WaitOne();
            }
        }

        public void SetGameLoopThread()
        {
            m_GameLoopThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
