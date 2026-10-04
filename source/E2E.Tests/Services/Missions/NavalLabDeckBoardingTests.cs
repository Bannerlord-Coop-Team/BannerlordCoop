#if DEBUG
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents.Packets;
using Missions.Battles;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabDeckBoardingTests : NavalMissionTestEnvironment
{
    private static readonly Vec3 Local = new Vec3(1f, 2f, 0.5f);

    public NavalLabDeckBoardingTests(ITestOutputHelper output) : base(output) { }

    private void StartReleasedOnB()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(First); Ready(Second); Tick(First); Tick(Second);
        Execute("complete-deployment"); Tick(First); Tick(Second);
        // naval-B observes naval-A's captain released at revision 2; B's own hull is support slot 1.
        Adapter(Second).ReleasedHelmRevision = 2;
        Adapter(Second).Frames = new[]
        {
            new MatrixFrame(Mat3.Identity, new Vec3(250f, 250f, 0f)),
            new MatrixFrame(Mat3.Identity, new Vec3(274f, 250f, 0f))
        };
    }

    // Receives one deck pose on naval-B and returns the native target after one interpolator tick, or null.
    private Vec2? ReceiveDeck(int combatant, long revision, int deckShip)
    {
        Vec2? target = null;
        Second.Call(() =>
        {
            Guid id = Manifest.Combatants[combatant];
            Assert.True(Second.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(id, out var info));
            Assert.True(AgentMirror.TryGet(info.Agent, out var mirror));
            mirror.Position = Adapter(Second).Frames[deckShip - 1].TransformToParent(Local);
            int targetWrites = mirror.SetTargetPositionAndDirectionCalls;
            var data = new AgentData(info.Agent);
            data.NavalHelmRevision = revision;
            data.StampDeck(Manifest.Ships[deckShip - 1], 1, Local, 0.5f);
            var handler = Adapter(Second).Controller!.AgentMovementHandler;
            handler.HandlePacket(null, new MovementPacket(new[] { id }, new[] { data }, new[] { Manifest.Ships[deckShip - 1] }));
            handler.Interpolator.Tick(1f / 60f);
            Assert.Equal(0, mirror.TeleportToPositionCalls);
            if (handler.Interpolator.TryGetTargetFrame(info.Agent, out _, out _, out _)
                && mirror.SetTargetPositionAndDirectionCalls > targetWrites)
                target = mirror.LastTargetPosition;
        });
        return target;
    }

    [Fact]
    public void ForeignCaptainPose_ResolvesEitherFixtureSupportHull_ByOriginCaptainIdentity()
    {
        StartReleasedOnB();

        Vec2? onOwnHull = ReceiveDeck(0, 2, 1);
        Vec2? onReceiverHull = ReceiveDeck(0, 2, 2);

        Assert.Equal((Vec2?)Adapter(Second).Frames[0].TransformToParent(Local).AsVec2, onOwnHull);
        Assert.Equal((Vec2?)Adapter(Second).Frames[1].TransformToParent(Local).AsVec2, onReceiverHull);
        Assert.Equal(new[] { (0, 0), (0, 1) }, Adapter(Second).DeckFrameReads.Distinct());
    }

    [Theory]
    [InlineData("rower_on_foreign_hull")]
    [InlineData("reseated_late_pose")]
    [InlineData("stale_revision")]
    public void SupportHullNeverGrantsIdentity_LateOrNonCaptainPosesAreRejected(string condition)
    {
        StartReleasedOnB();
        int combatant = condition == "rower_on_foreign_hull" ? 1 : 0;
        long revision = condition == "stale_revision" ? 1 : 2;
        if (condition == "reseated_late_pose") Adapter(Second).ReleasedHelmRevision = -1;

        Assert.Null(ReceiveDeck(combatant, revision, 2));
        Assert.Empty(Adapter(Second).DeckFrameReads);
    }
}
#endif
