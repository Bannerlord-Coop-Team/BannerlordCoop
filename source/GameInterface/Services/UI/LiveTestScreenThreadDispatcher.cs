#if DEBUG
using Common;
using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace GameInterface.Services.UI;

public static class LiveTestScreenThreadDispatcher
{
    private static readonly ConcurrentQueue<WorkItem> Queue = new ConcurrentQueue<WorkItem>();

    internal static int QueueLength => Queue.Count;

    public static void Run(Action action) => Run(action, GameThread.BlockingTimeout);

    internal static void Run(Action action, TimeSpan timeout)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var workItem = new WorkItem(action);
        Queue.Enqueue(workItem);
        if (!workItem.Completion.Task.Wait(timeout))
        {
            workItem.Cancel();
            throw new TimeoutException("A screen-thread action was not processed within the timeout.");
        }

        if (workItem.Exception != null)
        {
            ExceptionDispatchInfo.Capture(workItem.Exception).Throw();
        }
    }

    internal static void Update()
    {
        while (Queue.TryDequeue(out WorkItem workItem))
        {
            workItem.Execute();
        }
    }

    private sealed class WorkItem
    {
        private readonly Action action;
        private int canceled;

        public WorkItem(Action action)
        {
            this.action = action;
        }

        public TaskCompletionSource<bool> Completion { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception Exception { get; private set; }

        public void Cancel() => Interlocked.Exchange(ref canceled, 1);

        public void Execute()
        {
            if (Volatile.Read(ref canceled) != 0)
            {
                Completion.TrySetResult(false);
                return;
            }

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Exception = exception;
            }
            finally
            {
                Completion.TrySetResult(true);
            }
        }
    }
}
#endif
