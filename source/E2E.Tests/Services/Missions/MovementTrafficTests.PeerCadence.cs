using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.PacketHandlers;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Messages;
using Missions.Services.Network;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

// #4494: each peer gets min(its cap, bulk Hz, frame rate) whatever frame it joined on and however long the battle runs.
public partial class MovementTrafficTests
{
    [Theory]
    [InlineData(60, 40, 40, 40)]
    [InlineData(60, 40, 40, 30)]
    [InlineData(60, 60, 60, 40)]
    [InlineData(45, 60, 60, 40)]
    public void PollMovement_PeerJoiningBetweenBulkPollsGetsItsOwnCap(
        int framesPerSecond,
        int bulkHz,
        int earlyCapHz,
        int lateCapHz)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            using AgentMovementHandler handler = CreateHandlerWithFrozenCapClock(peer);
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", Guid.NewGuid(), 1, agent));
            handler.Configure(MovementCadenceProfile.Battle);
            Assert.True(handler.TrySetForcedBulkHz(bulkHz, out _));
            broker.Publish(this, new NetworkMissionPeerEntered("early", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("early", earlyCapHz, 1));

            float frameSeconds = 1f / framesPerSecond;
            handler.PollMovement(0f);
            handler.PollMovement(frameSeconds);
            // The cap goes after the enter, because entering clears the peer's cap.
            broker.Publish(this, new NetworkMissionPeerEntered("late", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("late", lateCapHz, 1));

            for (int frame = 1; frame <= 3 * framesPerSecond; frame++)
            {
                if (frame == framesPerSecond + 1)
                    network.DirectPacketSends.Clear();
                mirror.Position = new Vec3(frame, 0f, 0f);
                handler.PollMovement(frameSeconds);
            }

            int earlyHz = Math.Min(earlyCapHz, Math.Min(bulkHz, framesPerSecond));
            int lateHz = Math.Min(lateCapHz, Math.Min(bulkHz, framesPerSecond));
            Assert.Equal(2 * earlyHz, network.DirectPacketSends.Count(send =>
                send.ControllerId == "early" && send.Packet is MovementPacket));
            Assert.Equal(2 * lateHz, network.DirectPacketSends.Count(send =>
                send.ControllerId == "late" && send.Packet is MovementPacket));
            MovementRateSnapshot rate = handler.MovementRate;
            Assert.Equal(earlyHz, rate.PeerBulkHz["early"]);
            Assert.Equal(lateHz, rate.PeerBulkHz["late"]);
        });
    }

    [Fact]
    public void PollMovement_LongBattleKeepsPeerAtItsCap()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            using AgentMovementHandler handler = CreateHandlerWithFrozenCapClock(peer);
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", Guid.NewGuid(), 1, agent));
            handler.Configure(MovementCadenceProfile.Battle);
            Assert.True(handler.TrySetForcedBulkHz(40, out _));
            broker.Publish(this, new NetworkMissionPeerEntered("early", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("early", 40, 1));

            handler.PollMovement(0f);
            // 700 seconds, where float frame time used to drop the peer to 20 Hz.
            const int frames = 700 * 60;
            for (int frame = 1; frame <= frames; frame++)
            {
                if (frame == frames - 3600 + 1)
                    network.DirectPacketSends.Clear();
                mirror.Position = new Vec3(frame % 1000, 0f, 0f);
                handler.PollMovement(1f / 60f);
            }

            Assert.Equal(60 * 40, network.DirectPacketSends.Count(send =>
                send.ControllerId == "early" && send.Packet is MovementPacket));
        });
    }

    [Fact]
    public void PollMovement_BulkRateDroppingToPeerCapSendsAtMostOneOverCap()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            using AgentMovementHandler handler = CreateHandlerWithFrozenCapClock(peer);
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", Guid.NewGuid(), 1, agent));
            handler.Configure(MovementCadenceProfile.Battle);
            Assert.True(handler.TrySetForcedBulkHz(60, out _));
            broker.Publish(this, new NetworkMissionPeerEntered("receiver", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("receiver", 40, 1));

            handler.PollMovement(0f);
            double time = 1f / 45f;
            handler.PollMovement(1f / 45f);
            int sendsInWindow = 0;
            for (int frame = 1; frame <= 46; frame++)
            {
                // Bulk drops to the cap 23 ms after a capped send, so the first every-poll send must still wait.
                if (frame == 45)
                    Assert.True(handler.TrySetForcedBulkHz(40, out _));
                float frameSeconds = (1f / 45f) + (frame % 2 == 1 ? 0.001f : -0.001f);
                time += frameSeconds;
                int sentBefore = network.DirectPacketSends.Count;
                mirror.Position = new Vec3(frame, 0f, 0f);
                handler.PollMovement(frameSeconds);
                if (time >= 0.045 && time < 1.045)
                    sendsInWindow += network.DirectPacketSends.Count - sentBefore;
            }

            Assert.InRange(sendsInWindow, 40, 41);
        });
    }

    // Bulk drops from 60 Hz to the 40 Hz cap while the peer is still on its capped timer.
    [Theory]
    [InlineData(52, 0.0005f)]
    [InlineData(54, 0.001f)]
    public void PollMovement_BulkRateDroppingToPeerCapStaysOneOverCapInEverySecond(int dropFrame, float wobbleSeconds)
    {
        AssertAtMostOneOverCapInEverySecond(
            dropFrame + 90,
            frame => (1f / 45f) + (frame % 2 == 0 ? wobbleSeconds : -wobbleSeconds),
            frame => frame < dropFrame ? 60 : 40);
    }

    // The peer gets every 40 Hz poll at about 41 fps, then bulk rises above its 40 Hz cap.
    [Fact]
    public void PollMovement_BulkRateRisingAbovePeerCapStaysOneOverCapInEverySecond()
    {
        AssertAtMostOneOverCapInEverySecond(
            172,
            _ => 0.024389649f,
            frame => frame < 41 ? 10 : frame < 82 ? 40 : 60);
    }

    // A peer joins at a 15 Hz cap under 10 Hz bulk, then bulk goes to 60 Hz and its cap to 30 Hz a frame later.
    [Fact]
    public void PollMovement_PeerCapRisingStaysOneOverNewCapInEverySecond()
    {
        var run = BulkSendsToReceiver(
            78,
            frame => (1f / 35f) + (frame % 2 == 1 ? 0.0005f : -0.0005f),
            frame => frame < 7 ? 10 : 60,
            frame => frame < 8 ? 15 : 30,
            joinsAfterFirstPoll: true);

        Assert.InRange(MostInOneSecond(run.Sends), 30, 31);
    }

    // At a 60 Hz cap the peer gets every 40 Hz poll, then its cap drops to 30 Hz, under the bulk rate.
    [Fact]
    public void PollMovement_PeerCapDroppingUnderBulkRateKeepsItsNewCap()
    {
        var run = BulkSendsToReceiver(540, _ => 1f / 45f, _ => 40, frame => frame < 14 ? 60 : 30, joinsAfterFirstPoll: false);

        // Frames 181 to 540 are 8 s at 30 Hz.
        Assert.InRange(run.Sends.Count(send => send.Frame > 180), 239, 241);
        Assert.InRange(MostInOneSecond(run.Sends.Where(send => send.Frame >= 14).ToList()), 30, 31);
    }

    // Short frames whose float sums round: if the cadence clock isn't the frames' real sum, a 42nd send lands a few ns
    // inside the second.
    [Fact]
    public void PollMovement_RoundedShortFramesStayOneOverCapInEverySecond()
    {
        var run = BulkSendsToReceiver(
            81,
            frame => frame == 1 ? 0.0625f
                : frame == 2 ? 0.01f
                : frame == 3 ? (1f / 40f) - 0.01f
                : frame % 2 == 0 ? 0.002002f : (1f / 40f) - 0.002002f,
            _ => 60,
            _ => 40,
            joinsAfterFirstPoll: false);

        Assert.InRange(MostInOneSecond(run.Sends), 40, 41);
    }

    // One moving agent sent to a receiver capped at 40 Hz with bulk forced per frame: no second holds more than 41 bulk
    // sends, and the last second still gets the cap.
    private void AssertAtMostOneOverCapInEverySecond(int frames, Func<int, float> frameSecondsAt, Func<int, int> bulkHzAt)
    {
        var run = BulkSendsToReceiver(frames, frameSecondsAt, bulkHzAt, _ => 40, joinsAfterFirstPoll: false);

        Assert.InRange(MostInOneSecond(run.Sends), 40, 41);
        Assert.InRange(run.Sends.Count(send => send.Time > run.EndTime - 1d), 40, 41);
    }

    // Bulk Hz and the receiver's cap are set per frame (frame 0 is PollMovement(0f)); returns the frame and frame time of
    // every poll that sent the receiver bulk movement.
    private (List<(int Frame, double Time)> Sends, double EndTime) BulkSendsToReceiver(
        int frames,
        Func<int, float> frameSecondsAt,
        Func<int, int> bulkHzAt,
        Func<int, int> capHzAt,
        bool joinsAfterFirstPoll)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");
        var sends = new List<(int Frame, double Time)>();
        double time = 0d;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            using AgentMovementHandler handler = CreateHandlerWithFrozenCapClock(peer);
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", Guid.NewGuid(), 1, agent));
            handler.Configure(MovementCadenceProfile.Battle);
            int bulkHz = bulkHzAt(0);
            int capHz = capHzAt(0);
            long capSequence = 1;
            Assert.True(handler.TrySetForcedBulkHz(bulkHz, out _));
            if (joinsAfterFirstPoll)
                handler.PollMovement(0f);
            // The cap goes after the enter, because entering clears the peer's cap.
            broker.Publish(this, new NetworkMissionPeerEntered("receiver", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("receiver", capHz, capSequence));
            if (!joinsAfterFirstPoll)
                handler.PollMovement(0f);

            for (int frame = 1; frame <= frames; frame++)
            {
                if (bulkHzAt(frame) != bulkHz)
                {
                    bulkHz = bulkHzAt(frame);
                    Assert.True(handler.TrySetForcedBulkHz(bulkHz, out _));
                }
                if (capHzAt(frame) != capHz)
                {
                    capHz = capHzAt(frame);
                    broker.Publish(this, new NetworkMovementReceiverCap("receiver", capHz, ++capSequence));
                }
                float frameSeconds = frameSecondsAt(frame);
                time += frameSeconds;
                int sentBefore = network.DirectPacketSends.Count(send => send.ControllerId == "receiver");
                mirror.Position = new Vec3(frame, 0f, 0f);
                handler.PollMovement(frameSeconds);
                if (network.DirectPacketSends.Count(send => send.ControllerId == "receiver") > sentBefore)
                    sends.Add((frame, time));
            }
        });

        return (sends, time);
    }

    private static int MostInOneSecond(List<(int Frame, double Time)> sends) =>
        sends.Select((sent, index) => sends.Skip(index).TakeWhile(later => later.Time < sent.Time + 1d).Count()).Max();

    // Receiver caps expire after 3.5 s of Stopwatch time, so a frozen clock keeps a slow test run from dropping them.
    private static AgentMovementHandler CreateHandlerWithFrozenCapClock(EnvironmentInstance peer)
    {
        peer.Resolve<ICoopMissionComponent>().AgentMovementHandler.Dispose();
        var network = peer.Resolve<IBattleNetwork>();
        var broker = peer.Resolve<IMessageBroker>();
        var controllerIdProvider = peer.Resolve<IControllerIdProvider>();
        var missionContext = peer.Resolve<IMissionContext>();
        var rateController = new MovementRateController(
            network,
            broker,
            controllerIdProvider,
            missionContext,
            () => 0L,
            1,
            MovementRateController.ReadFrameLimitHz,
            enableHeartbeat: false,
            peer.Resolve<IMovementNetworkSettings>());
        return new AgentMovementHandler(
            network,
            peer.Resolve<IPacketManager>(),
            broker,
            peer.Resolve<INetworkAgentRegistry>(),
            controllerIdProvider,
            peer.Resolve<IAgentEquipmentApplier>(),
            peer.Resolve<IMovementBatchSender>(),
            peer.Resolve<IPuppetMountStateRepairer>(),
            peer.Resolve<IAgentVisualActionAccessor>(),
            rateController,
            peer.Resolve<IMovementPriorityScheduler>(),
            missionContext,
            peer.Resolve<INavalDeckMovementProbe>(),
            peer.Resolve<ILocationNpcAuthorityState>());
    }
}
