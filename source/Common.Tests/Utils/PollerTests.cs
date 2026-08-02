using Common.Util;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Tests.Utils;

public sealed class PollerTests
{
    [Fact]
    public async Task StopAndWait_WaitsForTheInFlightTickToFinish()
    {
        using var tickEntered = new ManualResetEventSlim(false);
        using var releaseTick = new ManualResetEventSlim(false);
        using var stopStarted = new ManualResetEventSlim(false);
        var poller = new Poller(_ =>
        {
            tickEntered.Set();
            releaseTick.Wait();
        }, TimeSpan.FromSeconds(1));

        try
        {
            poller.Start();
            Assert.True(tickEntered.Wait(TimeSpan.FromSeconds(5)));

            Task<bool> stopTask = Task.Run(() =>
            {
                stopStarted.Set();
                return poller.StopAndWait(TimeSpan.FromSeconds(5));
            });
            Assert.True(stopStarted.Wait(TimeSpan.FromSeconds(5)));
            Task firstCompleted = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.NotSame(stopTask, firstCompleted);

            releaseTick.Set();

            Assert.True(await stopTask);
        }
        finally
        {
            releaseTick.Set();
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void StopAndWait_CalledFromThePollingFunction_DoesNotWaitOnItself()
    {
        using var callbackCompleted = new ManualResetEventSlim(false);
        bool? stoppedDuringCallback = null;
        Poller poller = null!;
        poller = new Poller(_ =>
        {
            stoppedDuringCallback = poller.StopAndWait(TimeSpan.FromSeconds(5));
            callbackCompleted.Set();
        }, TimeSpan.FromSeconds(1));

        try
        {
            poller.Start();

            Assert.True(callbackCompleted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(stoppedDuringCallback);
            Assert.True(poller.StopAndWait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }
}
