using Coop.Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Coop.Game
{
    class GameLoopRunner : IUpdateable
    {
        private static readonly Lazy<GameLoopRunner> m_Instance = new Lazy<GameLoopRunner>(() => new GameLoopRunner());
        public static GameLoopRunner Instance
        {
            get
            {
                return m_Instance.Value;
            }
        }

        public static void RunOnMainThread(Action action, bool bBlocking = true)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId == Instance.m_GameLoopThreadId)
            {
                action();
                return;
            }
            else
            {
                EventWaitHandle ewh = bBlocking ? new EventWaitHandle(false, EventResetMode.ManualReset) : null;
                lock (Instance.m_QueueLock)
                {
                    Instance.m_Queue.Add((action, ewh));
                }
                ewh?.WaitOne();
            }
        }
        public void SetGameLoopThread()
        {
            m_GameLoopThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        public void Update(TimeSpan frameTime)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId != Instance.m_GameLoopThreadId)
            {
                throw new ArgumentException("Wrong thread!");
            }
            List<(Action, EventWaitHandle)> toBeRun = new List<(Action, EventWaitHandle)>();
            lock(m_Queue)
            {
                toBeRun.AddRange(m_Queue);
                m_Queue.Clear();
            }

            foreach ((Action, EventWaitHandle) task in toBeRun)
            {
                task.Item1.Invoke();
                task.Item2?.Set();
            }
        }
        private GameLoopRunner() { }
        private readonly object m_QueueLock = new object();
        private readonly List<(Action, EventWaitHandle)> m_Queue = new List<(Action, EventWaitHandle)>();
        private int m_GameLoopThreadId;
    }
}
