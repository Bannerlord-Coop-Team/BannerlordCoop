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

    [Fact]
    public void Start_WhileATickIsInFlight_RunsTheReplacementLoopOnlyAfterThatTickReturns()
    {
        using var firstTickEntered = new ManualResetEventSlim(false);
        using var releaseFirstTick = new ManualResetEventSlim(false);
        using var laterTickEntered = new ManualResetEventSlim(false);
        var sync = new object();
        int ticksEntered = 0;
        int ticksInFlight = 0;
        int mostTicksInFlight = 0;
        var poller = new Poller(_ =>
        {
            bool isFirstTick;
            lock (sync)
            {
                isFirstTick = ++ticksEntered == 1;
                ticksInFlight++;
                if (ticksInFlight > mostTicksInFlight) mostTicksInFlight = ticksInFlight;
            }

            if (isFirstTick)
            {
                firstTickEntered.Set();
                releaseFirstTick.Wait();
            }
            else
            {
                laterTickEntered.Set();
            }

            lock (sync) ticksInFlight--;
        }, TimeSpan.FromSeconds(1));

        try
        {
            poller.Start();
            Assert.True(firstTickEntered.Wait(TimeSpan.FromSeconds(5)));

            poller.Start();

            Assert.False(laterTickEntered.Wait(TimeSpan.FromSeconds(1)));

            releaseFirstTick.Set();

            Assert.True(laterTickEntered.Wait(TimeSpan.FromSeconds(5)));
            lock (sync) Assert.Equal(1, mostTicksInFlight);
        }
        finally
        {
            releaseFirstTick.Set();
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task StopAndWait_AfterARestart_StillWaitsForTheReplacedLoop()
    {
        using var firstTickEntered = new ManualResetEventSlim(false);
        using var releaseFirstTick = new ManualResetEventSlim(false);
        using var stopStarted = new ManualResetEventSlim(false);
        int ticksEntered = 0;
        var poller = new Poller(_ =>
        {
            if (Interlocked.Increment(ref ticksEntered) > 1) return;

            firstTickEntered.Set();
            releaseFirstTick.Wait();
        }, TimeSpan.FromSeconds(1));

        try
        {
            poller.Start();
            Assert.True(firstTickEntered.Wait(TimeSpan.FromSeconds(5)));

            poller.Start();

            Task<bool> stopTask = Task.Run(() =>
            {
                stopStarted.Set();
                return poller.StopAndWait(TimeSpan.FromSeconds(5));
            });
            Assert.True(stopStarted.Wait(TimeSpan.FromSeconds(5)));
            Task firstCompleted = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.NotSame(stopTask, firstCompleted);

            releaseFirstTick.Set();

            Assert.True(await stopTask);
        }
        finally
        {
            releaseFirstTick.Set();
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task RepeatedRestarts_KeepASingleTickInFlightAndStopCompletely()
    {
        var sync = new object();
        int ticksEntered = 0;
        int ticksInFlight = 0;
        int mostTicksInFlight = 0;
        var poller = new Poller(_ =>
        {
            lock (sync)
            {
                ticksEntered++;
                ticksInFlight++;
                if (ticksInFlight > mostTicksInFlight) mostTicksInFlight = ticksInFlight;
            }

            Thread.Sleep(1);

            lock (sync) ticksInFlight--;
        }, TimeSpan.FromMilliseconds(1));

        try
        {
            for (int restart = 0; restart < 200; restart++)
            {
                poller.Start();
            }

            Assert.True(poller.StopAndWait(TimeSpan.FromSeconds(10)));

            int ticksWhenStopped;
            lock (sync) ticksWhenStopped = ticksEntered;

            await Task.Delay(TimeSpan.FromMilliseconds(200));

            lock (sync)
            {
                Assert.Equal(ticksWhenStopped, ticksEntered);
                Assert.True(mostTicksInFlight <= 1, $"{mostTicksInFlight} ticks ran at the same time");
            }
        }
        finally
        {
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Stop_DoesNotWaitForTheInFlightTick()
    {
        using var tickEntered = new ManualResetEventSlim(false);
        using var releaseTick = new ManualResetEventSlim(false);
        using var tickFinished = new ManualResetEventSlim(false);
        var poller = new Poller(_ =>
        {
            tickEntered.Set();
            releaseTick.Wait();
            tickFinished.Set();
        }, TimeSpan.FromSeconds(1));

        try
        {
            poller.Start();
            Assert.True(tickEntered.Wait(TimeSpan.FromSeconds(5)));

            Task stopTask = Task.Run(() => poller.Stop());
            Task firstCompleted = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(stopTask, firstCompleted);
            Assert.False(tickFinished.IsSet);
        }
        finally
        {
            releaseTick.Set();
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void IsRunning_FollowsStartAndStop()
    {
        using var tickEntered = new ManualResetEventSlim(false);
        var poller = new Poller(_ => tickEntered.Set(), TimeSpan.FromMilliseconds(1));

        try
        {
            Assert.False(poller.IsRunning);

            poller.Start();
            Assert.True(tickEntered.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(poller.IsRunning);

            Assert.True(poller.StopAndWait(TimeSpan.FromSeconds(5)));
            Assert.False(poller.IsRunning);
        }
        finally
        {
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void Start_CalledFromThePollingFunction_RunsTheReplacementLoopOnItsOwn()
    {
        using var restartRequested = new ManualResetEventSlim(false);
        using var releaseFirstTick = new ManualResetEventSlim(false);
        using var laterTickEntered = new ManualResetEventSlim(false);
        var sync = new object();
        int ticksEntered = 0;
        int ticksInFlight = 0;
        int mostTicksInFlight = 0;
        Poller poller = null!;
        poller = new Poller(_ =>
        {
            bool isFirstTick;
            lock (sync)
            {
                isFirstTick = ++ticksEntered == 1;
                ticksInFlight++;
                if (ticksInFlight > mostTicksInFlight) mostTicksInFlight = ticksInFlight;
            }

            if (isFirstTick)
            {
                poller.Start();
                restartRequested.Set();
                releaseFirstTick.Wait();
            }
            else
            {
                laterTickEntered.Set();
            }

            lock (sync) ticksInFlight--;
        }, TimeSpan.FromSeconds(1));

        try
        {
            poller.Start();
            Assert.True(restartRequested.Wait(TimeSpan.FromSeconds(5)));

            Assert.False(laterTickEntered.Wait(TimeSpan.FromSeconds(1)));

            releaseFirstTick.Set();

            Assert.True(laterTickEntered.Wait(TimeSpan.FromSeconds(5)));
            lock (sync) Assert.Equal(1, mostTicksInFlight);
        }
        finally
        {
            releaseFirstTick.Set();
            poller.StopAndWait(TimeSpan.FromSeconds(5));
        }
    }
}
