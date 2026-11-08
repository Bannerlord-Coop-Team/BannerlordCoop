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

    [Theory]
    [InlineData(52, 0.0005f)]
    [InlineData(54, 0.001f)]
    public void PollMovement_BulkRateDroppingToPeerCapStaysOneOverCapInEverySecond(int dropFrame, float wobbleSeconds)
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
            var sendTimes = new List<double>();
            double time = 0d;
            for (int frame = 1; frame <= dropFrame + 90; frame++)
            {
                // The first every-poll send after the drop lands on the bulk schedule, not the capped timer's phase.
                if (frame == dropFrame)
                    Assert.True(handler.TrySetForcedBulkHz(40, out _));
                float frameSeconds = (1f / 45f) + (frame % 2 == 0 ? wobbleSeconds : -wobbleSeconds);
                time += frameSeconds;
                int sentBefore = network.DirectPacketSends.Count(send => send.ControllerId == "receiver");
                mirror.Position = new Vec3(frame, 0f, 0f);
                handler.PollMovement(frameSeconds);
                if (network.DirectPacketSends.Count(send => send.ControllerId == "receiver") > sentBefore)
                    sendTimes.Add(time);
            }

            int mostInOneSecond = sendTimes
                .Select((sent, index) => sendTimes.Skip(index).TakeWhile(later => later < sent + 1d).Count())
                .Max();
            Assert.InRange(mostInOneSecond, 40, 41);
            Assert.InRange(sendTimes.Count(sent => sent > time - 1d), 40, 41);
        });
    }

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
