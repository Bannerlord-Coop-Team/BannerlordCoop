using Common;
using Common.Network.Messages;
using Common.Network;
using Common.Messaging;
using GameInterface.Services.Voice;
using GameInterface.Services.UI.CoopOptions;
using System.Collections.Generic;
using Common.PacketHandlers;
using Common.Voice;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Voice;
using Coop.Tests.Mocks;
using Coop.Tests.Extensions;
using Coop.Tests.Stubs;
using GameInterface.Configuration;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using Serilog;
using System;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Server.Services.Voice;

public partial class ServerVoiceHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly PacketManager packets = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly PlayerManager players;
    private readonly MissionManager missions = new();
    private readonly ServerVoiceHandler handler;
    private long now = 1000;

    public ServerVoiceHandlerTests()
    {
        players = new PlayerManager(Mock.Of<ILogger>(), objects.Object, Mock.Of<IControllerIdProvider>());
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(() => now);
        var config = new Mock<IModConfig>();
        config.SetupGet(x => x.Data).Returns(new ModConfigData
        {
            Voice = new VoiceConfigData { MapFullVolumeDistance = 2, MapMaximumDistance = 12 }
        });
        var policy = new VoicePolicy();
        handler = new ServerVoiceHandler(packets, broker, network, players, objects.Object, missions, missions,
            new VoiceRoutingState(policy), policy, clock.Object, config.Object, () => new VoiceTransitWindow());
    }

    private (NetPeer peer, MobileParty party) Add(string id, float x = 0)
    {
        var peer = network.CreatePeer();
        peer.Setup(peer.Id, "127.0.0." + network.Peers.Count);
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.Party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.Party.MobileParty = party;
        party._position = new CampaignVec2(new Vec2(x, 0), true);
        objects.Setup(x => x.TryGetObject(id, out party)).Returns(true);
        players.AddPlayer(new Player(id, "hero:" + id, id, "clan:" + id, "character:" + id));
        players.SetPeer(id, peer);
        return (peer, party);
    }

    private void Context(NetPeer peer, string context = "campaign", long epoch = 1)
    {
        broker.Publish(peer, new VoiceContextChanged
        {
            Position = new VoicePosition(context, epoch, 0, 0, 0, true, true), SentAt = now
        });
        Drain();
    }

    private void Frame(NetPeer peer, string context = "campaign", long epoch = 1, uint sequence = 1,
        bool audio = true, bool speak = true, float x = 0)
    {
        packets.HandleReceive(peer, new VoicePacket
        {
            Position = new VoicePosition(context, epoch, x, 0, 0, speak, true),
            Sequence = sequence, StateSequence = sequence, SentAt = now,
            Audio = audio ? new byte[] { 1 } : Array.Empty<byte>()
        });
        Drain();
    }

    [Fact]
    public void CampaignUsesAuthoritativePartyDistanceNotClaimedCoordinates()
    {
        var speaker = Add("speaker");
        var near = Add("near", 7);
        var far = Add("far", 100);
        Context(speaker.peer); Context(near.peer); Context(far.peer);
        Frame(near.peer, audio: false); Frame(far.peer, audio: false);
        Frame(speaker.peer, x: 1000);
        var send = Assert.Single(network.ImmediateSends);
        Assert.Same(near.peer, send.Peer);
        Assert.Equal(0.5f, Assert.IsType<VoicePacket>(send.Payload).Gain);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SettlementMenusRetainCampaignVoiceInBothDirections(bool speakerInSettlement, bool listenerInSettlement)
    {
        var speaker = Add("speaker");
        var listener = Add("listener", 7);
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false);
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        if (speakerInSettlement) speaker.party._currentSettlement = settlement;
        if (listenerInSettlement) listener.party._currentSettlement = settlement;

        Frame(speaker.peer, x: 1000);
        var outgoing = Assert.IsType<VoicePacket>(Assert.Single(network.ImmediateSends).Payload);
        Assert.Same(listener.peer, network.ImmediateSends[0].Peer);
        Assert.Equal(0.5f, outgoing.Gain);
        Assert.Equal(0, outgoing.Position.X);
        Frame(listener.peer, sequence: 2, x: 1000);
        Assert.Equal(2, network.ImmediateSends.Count);
        Assert.Same(speaker.peer, network.ImmediateSends[1].Peer);
        var incoming = Assert.IsType<VoicePacket>(network.ImmediateSends[1].Payload);
        Assert.Equal(0.5f, incoming.Gain);
        Assert.Equal(7, incoming.Position.X);
    }

    [Theory]
    [InlineData("town|tavern")]
    [InlineData("tournament:session")]
    [InlineData("event")]
    public void MissionEntryImmediatelyRejectsStaleCampaignSenderAndListener(string instance)
    {
        var speaker = Add("speaker");
        var listener = Add("listener");
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false);
        Frame(speaker.peer);
        Assert.Single(network.ImmediateSends);

        Assert.True(missions.TryEnterMission(listener.peer, "listener", instance, out _));
        Frame(speaker.peer, sequence: 2);
        Frame(listener.peer, sequence: 2);
        Assert.Single(network.ImmediateSends);
        Assert.True(missions.TryLeaveMission(listener.peer, "listener", instance, out _));
        Frame(listener.peer, sequence: 3, audio: false);
        Frame(speaker.peer, sequence: 3);
        Assert.Equal(2, network.ImmediateSends.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapEventEntryStillRejectsCampaignSenderAndListener(bool speakerInBattle)
    {
        var speaker = Add("speaker");
        var listener = Add("listener");
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false);
        var party = speakerInBattle ? speaker.party : listener.party;
        party.Party._mapEventSide = new MapEventSide(
            (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent)),
            TaleWorlds.Core.BattleSideEnum.Attacker, party.Party);
        Frame(speaker.peer);
        Assert.Empty(network.ImmediateSends);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(7, 0.5f)]
    [InlineData(12, 0)]
    [InlineData(13, 0)]
    public void SettlementMenuUsesAuthoritativeCampaignDistanceBoundaries(float distance, float gain)
    {
        var speaker = Add("speaker");
        var listener = Add("listener", distance);
        speaker.party._currentSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false);
        Frame(speaker.peer, x: distance);
        if (gain == 0) Assert.Empty(network.ImmediateSends);
        else Assert.Equal(gain, Assert.IsType<VoicePacket>(Assert.Single(network.ImmediateSends).Payload).Gain);
    }

    [Theory]
    [InlineData("scene:town|tavern")]
    [InlineData("scene:tournament:session")]
    [InlineData("battle:event")]
    public void SceneRelayRequiresRealMembershipAndSpectatorRemainsReceiveOnly(string context)
    {
        var speaker = Add("speaker");
        var spectator = Add("spectator");
        var other = Add("other");
        string instance = context.Substring(context.IndexOf(':') + 1);
        missions.TryEnterMission(speaker.peer, "speaker", instance, out _);
        missions.TryEnterMission(spectator.peer, "spectator", instance, out _);
        missions.TryEnterMission(other.peer, "other", "different", out _);
        Context(speaker.peer, context); Context(spectator.peer, context); Context(other.peer, context);
        Frame(spectator.peer, context, speak: false, audio: false);
        Frame(other.peer, context, audio: false);
        Frame(speaker.peer, context);
        Assert.Same(spectator.peer, Assert.Single(network.ImmediateSends).Peer);
        Frame(spectator.peer, context, sequence: 2, speak: false);
        Assert.Single(network.ImmediateSends);
        missions.RevokeRelay(spectator.peer);
        Frame(speaker.peer, context, sequence: 2);
        Assert.Single(network.ImmediateSends);
    }

    [Fact]
    public void FreshEpochAndHeartbeatRequiredAfterTransitionAndDisconnect()
    {
        var speaker = Add("speaker"); var listener = Add("listener");
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false);
        Context(listener.peer, epoch: 2);
        Frame(speaker.peer);
        Assert.Empty(network.ImmediateSends);
        Frame(listener.peer, epoch: 1, sequence: 2, audio: false);
        Frame(speaker.peer, sequence: 2);
        Assert.Empty(network.ImmediateSends);
        Frame(listener.peer, epoch: 2, sequence: 3, audio: false);
        Frame(speaker.peer, sequence: 3);
        Assert.Equal(2, Assert.IsType<VoicePacket>(Assert.Single(network.ImmediateSends).Payload).ListenerEpoch);
        broker.Publish(listener.peer, new PlayerDisconnected(listener.peer, default)); Drain();
        Frame(speaker.peer, sequence: 4);
        Assert.Single(network.ImmediateSends);
        Context(listener.peer); Frame(listener.peer, audio: false);
        Frame(speaker.peer, sequence: 5);
        Assert.Equal(2, network.ImmediateSends.Count);
    }

    [Fact]
    public void ExpiredPositionAndAudioBeforeContextAreNotRelayed()
    {
        var speaker = Add("speaker"); var listener = Add("listener");
        Context(listener.peer); Frame(listener.peer, audio: false);
        Frame(speaker.peer);
        Assert.Empty(network.ImmediateSends);
        Context(speaker.peer); now += 501;
        Frame(speaker.peer, sequence: 2);
        Assert.Empty(network.ImmediateSends);
    }

    [Fact]
    public void TenNearbyPlayersRelayAllTalkersWithoutSelfEcho()
    {
        var participants = Enumerable.Range(0, 10).Select(index => Add("player" + index)).ToArray();
        foreach (var participant in participants)
        {
            Context(participant.peer);
            Frame(participant.peer, audio: false);
        }
        foreach (var participant in participants) Frame(participant.peer, sequence: 2);
        Assert.Equal(90, network.ImmediateSends.Count);
        foreach (var participant in participants)
            Assert.Equal(9, network.ImmediateSends.Count(send => send.Peer == participant.peer));
    }

    [Fact]
    public void ClientRelayClientPathDropsAudioBeforeContextAndFencesLaterTransition()
    {
        var speaker = Add("speaker");
        var listener = Add("listener", 7);
        var senderWire = new Queue<object>();
        var listenerWire = new Queue<object>();
        var senderAudio = new Mock<IVoiceAudio>();
        var receiverAudio = new Mock<IVoiceAudio>();
        VoiceInputSnapshot captured = null;
        senderAudio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((sample, _) => captured = sample);
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(() => now);
        string senderContext = "campaign";

        VoiceClient Client(Mock<IVoiceAudio> audio, Queue<object> wire, Func<string> context)
        {
            var input = new Mock<IVoiceGameInput>();
            input.Setup(x => x.Sample(It.IsAny<long>(), It.IsAny<VoiceSettings>())).Returns<long, VoiceSettings>((epoch, _) =>
                new VoiceInputSnapshot(new VoicePosition(context(), epoch, 0, 0, 0, true, true), true, false, true, now));
            var transport = new Mock<INetwork>();
            transport.Setup(x => x.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(message => wire.Enqueue(message));
            transport.Setup(x => x.SendAll(It.IsAny<IPacket>())).Callback<IPacket>(packet => wire.Enqueue(packet));
            var store = new Mock<ICoopOptionsStore>();
            store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
            var client = new VoiceClient(input.Object, audio.Object, clock.Object, transport.Object, store.Object, new VoiceTransitWindow());
            client.Configure(new VoiceRanges(), now);
            return client;
        }

        void Deliver(NetPeer peer, object payload)
        {
            if (payload is VoiceContextChanged context) broker.Publish(peer, context);
            else packets.HandleReceive(peer, Assert.IsType<VoicePacket>(payload));
            Drain();
        }

        using var sender = Client(senderAudio, senderWire, () => senderContext);
        using var receiver = Client(receiverAudio, listenerWire, () => "campaign");
        receiver.Tick();
        while (listenerWire.Count > 0) Deliver(listener.peer, listenerWire.Dequeue());
        sender.Tick();
        object reliableContext = senderWire.Dequeue();
        Deliver(speaker.peer, senderWire.Dequeue());
        senderAudio.Raise(x => x.Encoded += null, captured, new byte[] { 1 });
        Deliver(speaker.peer, senderWire.Dequeue());
        Assert.Empty(network.ImmediateSends);
        Deliver(speaker.peer, reliableContext);
        senderAudio.Raise(x => x.Encoded += null, captured, new byte[] { 2 });
        Deliver(speaker.peer, senderWire.Dequeue());
        var packet = Assert.IsType<VoicePacket>(Assert.Single(network.ImmediateSends).Payload);
        receiver.Receive(packet);
        receiverAudio.Verify(x => x.Receive(It.Is<VoicePacket>(p => p.Gain == 0.5f && p.ListenerEpoch == 1 && p.Audio[0] == 2)), Times.Once);

        senderAudio.Raise(x => x.Encoded += null, captured, new byte[] { 3 });
        object delayed = senderWire.Dequeue();
        senderContext = "scene:town|tavern";
        sender.Tick();
        Deliver(speaker.peer, senderWire.Dequeue());
        Deliver(speaker.peer, delayed);
        Assert.Single(network.ImmediateSends);
    }

    [Fact]
    public void JoinDistributesConfiguredRangesOnlyToJoiningPeerAndDisposeUnsubscribes()
    {
        var first = Add("first"); Add("second");
        broker.Publish(first.peer, new PlayerCampaignSynchronized(first.peer)); Drain();
        var send = Assert.Single(network.ImmediateSends);
        Assert.Same(first.peer, send.Peer);
        Assert.Equal(12, Assert.IsType<VoiceConfiguration>(send.Payload).Ranges.MapMaximum);
        handler.Dispose();
        broker.Publish(first.peer, new PlayerCampaignSynchronized(first.peer)); Drain();
        Assert.Single(network.ImmediateSends);
    }

    private void Drain() => GameThread.Run(() => { }, blocking: true, label: nameof(ServerVoiceHandlerTests));
    public void Dispose() { handler.Dispose(); network.Dispose(); broker.Dispose(); }
}
