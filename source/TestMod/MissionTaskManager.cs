using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CoopTestMod
{
    public class MissionTaskManager
    {
        private static ConcurrentQueue<(object, Action<object>)> Queue { get; set; }
        private static MissionTaskManager missionQueueManager;

        private MissionTaskManager() {  }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static MissionTaskManager Instance()
        {
            if(missionQueueManager == null)
            {
                missionQueueManager = new MissionTaskManager();
                Queue = new ConcurrentQueue<(object, Action<object>)>();
            }
            return missionQueueManager;
        }

        public void AddTask(object task,  Action<object> action)
        {
            Queue.Enqueue((task, action));    
        }

        public void ApplyPendingTasks()
        {
            while (!Queue.IsEmpty)
            {
                (object, Action<object>) task;
                Queue.TryDequeue(out task);
                task.Item2.Invoke(task.Item1);
            }
        }

        public void Clear()
        {
            while (!Queue.IsEmpty)
            {
                Queue.TryDequeue(out _);
            }
        }
    }


}
