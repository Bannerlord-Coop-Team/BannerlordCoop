using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
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

// #4494: each peer gets min(its cap, bulk Hz) whatever frame it joined on and however long the battle runs.
public partial class MovementTrafficTests
{
    private const float SixtyFps = 1f / 60f;

    [Theory]
    [InlineData(40, 40, 40)]
    [InlineData(40, 40, 30)]
    [InlineData(60, 60, 40)]
    public void PollMovement_PeerJoiningBetweenBulkPollsGetsItsOwnCap(
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
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var handler = peer.Resolve<ICoopMissionComponent>().AgentMovementHandler;
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), 1, agent));
            handler.Configure(MovementCadenceProfile.Battle);
            Assert.True(handler.TrySetForcedBulkHz(bulkHz, out _));
            broker.Publish(this, new NetworkMissionPeerEntered("early", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("early", earlyCapHz, 1));

            handler.PollMovement(0f);
            handler.PollMovement(SixtyFps);
            // The cap goes after the enter, because entering clears the peer's cap.
            broker.Publish(this, new NetworkMissionPeerEntered("late", "battle"));
            broker.Publish(this, new NetworkMovementReceiverCap("late", lateCapHz, 1));

            for (int frame = 1; frame <= 180; frame++)
            {
                if (frame == 61)
                    network.DirectPacketSends.Clear();
                // Refresh the caps once a second like the receiver heartbeat, so they never expire mid-test.
                if (frame % 60 == 0)
                {
                    broker.Publish(this, new NetworkMovementReceiverCap("early", earlyCapHz, 1 + (frame / 60)));
                    broker.Publish(this, new NetworkMovementReceiverCap("late", lateCapHz, 1 + (frame / 60)));
                }
                mirror.Position = new Vec3(frame, 0f, 0f);
                handler.PollMovement(SixtyFps);
            }

            Assert.Equal(2 * Math.Min(earlyCapHz, bulkHz), CountMovementPackets(network, "early"));
            Assert.Equal(2 * Math.Min(lateCapHz, bulkHz), CountMovementPackets(network, "late"));
            MovementRateSnapshot rate = handler.MovementRate;
            Assert.Equal(Math.Min(earlyCapHz, bulkHz), rate.PeerBulkHz["early"]);
            Assert.Equal(Math.Min(lateCapHz, bulkHz), rate.PeerBulkHz["late"]);
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
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var handler = peer.Resolve<ICoopMissionComponent>().AgentMovementHandler;
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            Agent agent = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), 1, agent));
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
                if (frame % 60 == 0)
                    broker.Publish(this, new NetworkMovementReceiverCap("early", 40, 1 + (frame / 60)));
                mirror.Position = new Vec3(frame % 1000, 0f, 0f);
                handler.PollMovement(SixtyFps);
            }

            Assert.Equal(60 * 40, CountMovementPackets(network, "early"));
        });
    }

    private static int CountMovementPackets(MockBattleNetwork network, string controllerId) =>
        network.DirectPacketSends.Count(send =>
            send.ControllerId == controllerId && send.Packet is MovementPacket);
}
