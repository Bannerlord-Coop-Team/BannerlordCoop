#if DEBUG
using GameInterface.Services.UI;
using System;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(nameof(LiveTestScreenThreadDispatcherCollection))]
public class LiveTestScreenThreadDispatcherTests
{
    [Fact]
    public void Update_ExecutesQueuedActionOnCallingThread()
    {
        int executingThreadId = 0;
        Exception dispatchException = null;
        var worker = new Thread(() =>
        {
            try
            {
                LiveTestScreenThreadDispatcher.Run(
                    () => executingThreadId = Thread.CurrentThread.ManagedThreadId,
                    TimeSpan.FromSeconds(5));
            }
            catch (Exception exception)
            {
                dispatchException = exception;
            }
        });

        worker.Start();
        Assert.True(
            SpinWait.SpinUntil(() => LiveTestScreenThreadDispatcher.QueueLength == 1, TimeSpan.FromSeconds(5)),
            "the screen-thread action was not queued");

        int updateThreadId = Thread.CurrentThread.ManagedThreadId;
        LiveTestScreenThreadDispatcher.Update();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "the dispatching thread did not complete");

        Assert.Null(dispatchException);
        Assert.Equal(updateThreadId, executingThreadId);
    }

    [Fact]
    public void TimedOutAction_IsNotExecutedByLaterUpdate()
    {
        bool executed = false;

        Assert.Throws<TimeoutException>(() =>
            LiveTestScreenThreadDispatcher.Run(() => executed = true, TimeSpan.FromMilliseconds(10)));

        LiveTestScreenThreadDispatcher.Update();

        Assert.False(executed);
    }
}

[CollectionDefinition(nameof(LiveTestScreenThreadDispatcherCollection), DisableParallelization = true)]
public class LiveTestScreenThreadDispatcherCollection
{
}
#endif
