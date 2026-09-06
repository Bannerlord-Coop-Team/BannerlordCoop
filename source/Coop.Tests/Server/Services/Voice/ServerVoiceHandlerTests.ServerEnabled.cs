using Common.Network.Messages;
using Common.Voice;
using Coop.Tests.Extensions;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Voice;

public partial class ServerVoiceHandlerTests
{
    [Fact]
    public void DisableStopsCurrentAndFuturePeersUntilFreshContextsAfterEnable()
    {
        var speaker = Add("platform:1"); var listener = Add("platform:2");
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false); Frame(speaker.peer);
        var first = Assert.IsType<VoicePacket>(Assert.Single(network.ImmediateSends).Payload);
        Assert.Equal("platform:1", first.Speaker);
        Assert.True(first.StreamGeneration > 0);
        broker.Publish(this, new ModConfigApplied(new ModOptions(new ModOptionsData { VoiceEnabled = false })));
        Frame(speaker.peer, sequence: 2);
        var joined = Add("platform:3"); Context(joined.peer); Frame(joined.peer);
        Assert.Single(network.ImmediateSends);
        broker.Publish(this, new ModConfigApplied(new ModOptions(new ModOptionsData { VoiceEnabled = true })));
        Frame(listener.peer, sequence: 3, audio: false); Frame(speaker.peer, sequence: 3);
        Assert.Single(network.ImmediateSends);
        Context(listener.peer, epoch: 2); Context(speaker.peer, epoch: 2); Context(joined.peer, epoch: 2);
        Frame(listener.peer, epoch: 2, sequence: 4, audio: false);
        Frame(joined.peer, epoch: 2, sequence: 4, audio: false);
        Frame(speaker.peer, epoch: 2, sequence: 4);
        Assert.Equal(3, network.ImmediateSends.Count);
    }

    [Fact]
    public void DisableDiscardsQueuedSpeechAcrossImmediateReenable()
    {
        var speaker = Add("platform:1"); var listener = Add("platform:2");
        Context(speaker.peer); Context(listener.peer); Frame(listener.peer, audio: false);
        packets.HandleReceive(speaker.peer, new VoicePacket
        {
            Position = new VoicePosition("campaign", 1, 0, 0, 0, true, true),
            Audio = new byte[] { 1 }, Sequence = 1, StateSequence = 1, SentAt = now
        });
        broker.Publish(this, new ModConfigApplied(new ModOptions(new ModOptionsData { VoiceEnabled = false })));
        broker.Publish(this, new ModConfigApplied(new ModOptions(new ModOptionsData { VoiceEnabled = true })));
        Drain();
        Assert.Empty(network.ImmediateSends);
    }

    [Fact]
    public void ReplacementPlatformPeerGetsNewGenerationAndOldContextAndDisconnectCannotRemoveIt()
    {
        var speaker = Add("platform:1"); var listener = Add("platform:2");
        for (int index = 3; index <= 10; index++) Context(Add("platform:" + index).peer);
        Context(speaker.peer, epoch: 20); Context(listener.peer); Frame(listener.peer, audio: false);
        Frame(speaker.peer, epoch: 20);
        long first = Assert.IsType<VoicePacket>(Assert.Single(network.ImmediateSends).Payload).StreamGeneration;
        var replacement = network.CreatePeer(); replacement.Setup(replacement.Id, "127.0.0.99");
        players.SetPeer("platform:1", replacement);
        Context(replacement); Frame(replacement);
        var second = Assert.IsType<VoicePacket>(network.ImmediateSends.Last().Payload);
        Assert.Equal("platform:1", second.Speaker);
        Assert.True(second.StreamGeneration > first);
        Context(speaker.peer, epoch: 50);
        broker.Publish(speaker.peer, new PlayerDisconnected(speaker.peer, default)); Drain();
        Frame(replacement, sequence: 2);
        Assert.Equal(3, network.ImmediateSends.Count);
        Assert.Equal(second.StreamGeneration, Assert.IsType<VoicePacket>(network.ImmediateSends.Last().Payload).StreamGeneration);
    }
}
