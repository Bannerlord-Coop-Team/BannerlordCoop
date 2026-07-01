using Common.Util;

namespace Common.Tests.Utils
{
    public class PollerTests
    {
        [Fact]
        public void StopAndWait_BlocksUntilInFlightPollCompletes()
        {
            // The poll enters, then parks until the test releases it — simulating a poll iteration that is
            // still running when teardown asks the poller to stop.
            var pollEntered = new ManualResetEventSlim(false);
            var releasePoll = new ManualResetEventSlim(false);
            var pollCompleted = false;

            var poller = new Poller(_ =>
            {
                pollEntered.Set();
                releasePoll.Wait();
                Volatile.Write(ref pollCompleted, true);
            }, TimeSpan.FromMilliseconds(5));
            poller.Start();

            Assert.True(pollEntered.Wait(TimeSpan.FromSeconds(5)), "poll never started");

            // StopAndWait must not return while the poll iteration is still in flight.
            var stopTask = Task.Run(() => poller.StopAndWait(TimeSpan.FromSeconds(5)));
            Assert.False(stopTask.Wait(TimeSpan.FromMilliseconds(200)), "StopAndWait returned before the in-flight poll finished");
            Assert.False(Volatile.Read(ref pollCompleted));

            // Releasing the poll lets the iteration finish; only then may StopAndWait return.
            releasePoll.Set();
            Assert.True(stopTask.Wait(TimeSpan.FromSeconds(5)), "StopAndWait did not return after the poll finished");
            Assert.True(Volatile.Read(ref pollCompleted));
        }

        [Fact]
        public void StopAndWait_NoPollRunsAfterItReturns()
        {
            var count = 0;
            var poller = new Poller(_ => Interlocked.Increment(ref count), TimeSpan.FromMilliseconds(5));
            poller.Start();

            Thread.Sleep(50);
            poller.StopAndWait(TimeSpan.FromSeconds(5));

            // The join guarantees any in-flight iteration finished before StopAndWait returned, so the
            // count is now final — no later poll can bump it.
            var settled = Volatile.Read(ref count);
            Thread.Sleep(50);
            Assert.Equal(settled, Volatile.Read(ref count));
        }
    }
}
