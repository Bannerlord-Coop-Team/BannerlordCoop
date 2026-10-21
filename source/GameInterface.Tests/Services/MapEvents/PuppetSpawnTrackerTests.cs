using GameInterface.Services.MapEvents;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests that <see cref="PuppetSpawnTracker"/> scopes unwind to the state they opened on, per thread.</summary>
public class PuppetSpawnTrackerTests
{
    // A nested spawn scope closing must leave the outer spawn marked, not reset it to false.
    [Fact]
    public void BeginPuppetSpawn_NestedScopeRestoresOuterSpawn()
    {
        var tracker = new PuppetSpawnTracker();

        Assert.False(tracker.IsSpawningPuppet);
        using (tracker.BeginPuppetSpawn())
        {
            using (tracker.BeginPuppetSpawn())
            {
                Assert.True(tracker.IsSpawningPuppet);
            }

            Assert.True(tracker.IsSpawningPuppet);
        }

        Assert.False(tracker.IsSpawningPuppet);
    }

    // A spawn open on one thread is invisible to another, matching the single-call scope it wraps.
    [Fact]
    public void BeginPuppetSpawn_IsNotVisibleOnOtherThreads()
    {
        var tracker = new PuppetSpawnTracker();

        using (tracker.BeginPuppetSpawn())
        {
            bool seenElsewhere = true;
            var other = new Thread(() => seenElsewhere = tracker.IsSpawningPuppet);
            other.Start();
            other.Join();

            Assert.False(seenElsewhere);
            Assert.True(tracker.IsSpawningPuppet);
        }
    }
}
