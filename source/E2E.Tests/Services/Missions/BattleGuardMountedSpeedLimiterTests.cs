#if DEBUG
using System.Linq;
using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleGuardMountedSpeedLimiterTests : MissionTestEnvironment
{
    public BattleGuardMountedSpeedLimiterTests(ITestOutputHelper output)
        : base(output, numClients: 1) { }

    [Fact]
    public void ApplyAndRestore_PreservesOriginalAbsoluteLimit()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var mount = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(mount, out var mirror));
            mirror.MaximumSpeedLimit = 9.25f;
            var limiter =
                new BattleGuardFixture.BattleGuardMountedSpeedLimiter();

            limiter.Apply(mount);

            Assert.Equal(
                BattleGuardFixture.BattleGuardMountedSpeedLimiter.MaximumSpeed,
                mirror.MaximumSpeedLimit);
            Assert.False(mirror.LastMaximumSpeedLimitIsMultiplier);
            Assert.Equal(1, mirror.SetMaximumSpeedLimitCalls);

            limiter.Apply(mount);

            Assert.Equal(1, mirror.SetMaximumSpeedLimitCalls);

            mirror.MaximumSpeedLimit = -1f;
            limiter.Apply(mount);

            Assert.Equal(
                BattleGuardFixture.BattleGuardMountedSpeedLimiter.MaximumSpeed,
                mirror.MaximumSpeedLimit);
            Assert.Equal(2, mirror.SetMaximumSpeedLimitCalls);

            limiter.Restore();

            Assert.Equal(9.25f, mirror.MaximumSpeedLimit);
            Assert.False(mirror.LastMaximumSpeedLimitIsMultiplier);
            Assert.Equal(3, mirror.SetMaximumSpeedLimitCalls);

            limiter.Restore();

            Assert.Equal(3, mirror.SetMaximumSpeedLimitCalls);
        });
    }
}
#endif
