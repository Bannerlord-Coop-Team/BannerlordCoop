using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Network;
using GameInterface.Services.GameState.Messages;
using LiteNetLib;
using System.Linq;
using Xunit;

namespace Coop.Tests.Client.Network;

public class LoadingPacketBufferTests
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly LoadingPacketBuffer buffer;

    public LoadingPacketBufferTests()
    {
        buffer = new LoadingPacketBuffer(messageBroker);
    }

    /// <summary>Minimal <see cref="IPacket"/> stub carrying only a <see cref="PacketType"/>.</summary>
    private sealed class FakePacket : IPacket
    {
        public PacketType PacketType { get; }
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;
        public FakePacket(PacketType type) => PacketType = type;
    }

    [Fact]
    public void BeforeSave_GameplayPacket_PassesThrough()
    {
        // Not loading yet → handled immediately (Intercept returns false).
        Assert.False(buffer.Intercept(null, new FakePacket(PacketType.Message)));
    }

    [Fact]
    public void SaveDataPacket_PassesThrough_AndArmsBuffering()
    {
        // The save itself must be handled (it drives the load)...
        Assert.False(buffer.Intercept(null, new FakePacket(PacketType.SaveData)));
        // ...but subsequent gameplay packets are now buffered.
        Assert.True(buffer.Intercept(null, new FakePacket(PacketType.Message)));
    }

    [Fact]
    public void PacketWrapper_PassesThrough_EvenWhileBuffering()
    {
        buffer.Intercept(null, new FakePacket(PacketType.SaveData)); // arm

        Assert.False(buffer.Intercept(null, new FakePacket(PacketType.PacketWrapper)));
    }

    [Fact]
    public void Drain_ReplaysBufferedPacketsInFifoOrder_OnCampaignEntered()
    {
        buffer.Intercept(null, new FakePacket(PacketType.SaveData)); // arm

        var first = new FakePacket(PacketType.Message);
        var second = new FakePacket(PacketType.UpdatePartyBehavior);
        Assert.True(buffer.Intercept(null, first));
        Assert.True(buffer.Intercept(null, second));

        // Nothing drains until the campaign is ready.
        Assert.Empty(buffer.DrainIfRequested());

        messageBroker.Publish(this, new ClientCampaignEntered());

        var drained = buffer.DrainIfRequested().Select(item => item.Packet).ToArray();
        Assert.Equal(new IPacket[] { first, second }, drained);

        // After draining, buffering is off → live packets pass straight through again.
        Assert.False(buffer.Intercept(null, new FakePacket(PacketType.Message)));
    }

    [Fact]
    public void MainMenuEntered_ClearsBacklog_AndDisarms()
    {
        buffer.Intercept(null, new FakePacket(PacketType.SaveData)); // arm
        Assert.True(buffer.Intercept(null, new FakePacket(PacketType.Message))); // buffered

        // Disconnect/abort: drop the backlog and disarm.
        messageBroker.Publish(this, new MainMenuEntered());

        // A later campaign-entered must not replay the dropped backlog, and the gate is off.
        messageBroker.Publish(this, new ClientCampaignEntered());
        Assert.Empty(buffer.DrainIfRequested());
        Assert.False(buffer.Intercept(null, new FakePacket(PacketType.Message)));
    }
}
