using Common.Util;
using GameInterface.Services.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests that <see cref="ReplicatedDeathTracker"/> answers only for the dying agent inside its scope.</summary>
public class ReplicatedDeathTrackerTests
{
    // Inside the scope the dying agent gets the owner's affector, killing blow and state; other agents get nothing.
    [Fact]
    public void BeginReplicatedDeath_AnswersOnlyForTheDyingAgent()
    {
        var tracker = new ReplicatedDeathTracker();
        var dying = ObjectHelper.SkipConstructor<Agent>();
        var other = ObjectHelper.SkipConstructor<Agent>();
        var affector = ObjectHelper.SkipConstructor<Agent>();
        var killingBlow = new KillingBlow(new Blow(-1) { InflictedDamage = 40 }, Vec3.Zero, Vec3.Zero, 7, 0);

        using (tracker.BeginReplicatedDeath(dying, affector, killingBlow, AgentState.Unconscious))
        {
            Assert.True(tracker.IsReplicatedDeath(dying));
            Assert.True(tracker.TryGetReplicatedDeath(dying, out var actualAffector, out var actualBlow));
            Assert.Same(affector, actualAffector);
            Assert.Equal(7, actualBlow.DeathAction);
            Assert.True(tracker.TryGetReplicatedDeathState(dying, out var state));
            Assert.Equal(AgentState.Unconscious, state);

            Assert.False(tracker.IsReplicatedDeath(other));
            Assert.False(tracker.TryGetReplicatedDeathState(other, out var otherState));
            Assert.Equal(AgentState.None, otherState);
        }

        Assert.False(tracker.IsReplicatedDeath(dying));
    }

    // A nested death closing must hand the outer death back, not clear it.
    [Fact]
    public void BeginReplicatedDeath_NestedScopeRestoresOuterDeath()
    {
        var tracker = new ReplicatedDeathTracker();
        var outer = ObjectHelper.SkipConstructor<Agent>();
        var inner = ObjectHelper.SkipConstructor<Agent>();

        using (tracker.BeginReplicatedDeath(outer, null, default, AgentState.Killed))
        {
            using (tracker.BeginReplicatedDeath(inner, null, default, AgentState.Unconscious))
            {
                Assert.True(tracker.IsReplicatedDeath(inner));
                Assert.False(tracker.IsReplicatedDeath(outer));
            }

            Assert.True(tracker.TryGetReplicatedDeathState(outer, out var state));
            Assert.Equal(AgentState.Killed, state);
        }
    }
}
