using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Commands;
using GameInterface.Services.CampaignService.Messages;
using Moq;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

[Collection("ModConfigSerial")]
public class VoiceServerOptionsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerVoiceOptionRoundTripsAndCopyPreservesOtherOptions(bool enabled)
    {
        var original = new ModOptions(new ModOptionsData { AutoPauseEnabled = false, WandererLimit = 75 });
        Assert.True(original.VoiceEnabled);
        var options = new ModOptions(original, enabled);
        var received = Serializer.DeepClone(options);
        Assert.Equal(enabled, received.VoiceEnabled);
        Assert.False(received.AutoPauseEnabled);
        Assert.Equal(75, received.WandererLimit);
    }

    [Fact]
    public void NewJoinReceivesCurrentRuntimeOptionRatherThanOriginalStartupFile()
    {
        var previous = ModConfigProvider.ModOptions;
        try
        {
            var network = new Mock<INetwork>();
            using var broker = new Coop.Tests.Stubs.StubMessageBroker();
            var config = new Mock<IModConfig>();
            config.SetupGet(x => x.Data).Returns(new ModConfigData());
            using var handler = new global::GameInterface.Services.CampaignService.Handlers.LoadModConfigHandler(
                broker, network.Object, config.Object,
                Mock.Of<global::GameInterface.Services.Heroes.Interaces.ITimeControlInterface>());
            ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { VoiceEnabled = false });
            broker.Publish(this, new NetworkRequestServerModConfig());
            GameThread.Run(() => { }, blocking: true);
            network.Verify(x => x.Send(It.IsAny<LiteNetLib.NetPeer>(),
                It.Is<NetworkLoadModConfig>(message => !message.ModOptions.VoiceEnabled)), Times.Once);
        }
        finally { ModConfigProvider.ModOptions = previous; }
    }

    [Fact]
    public void ServerCommandPublishesAndBroadcastsCurrentOptionButRejectsClientAndInvalidInput()
    {
        bool server = ModInformation.IsServer;
        var previous = ModConfigProvider.ModOptions;
        try
        {
            var network = new Mock<INetwork>();
            var broker = new Mock<IMessageBroker>();
            var command = new ModOptionsCommands.VoiceEnabledCoopCommand(network.Object, broker.Object);
            Assert.Equal(CoopCommandSide.Server, command.Side);
            var args = new CoopCommandArgsFactory();
            ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData());
            ModInformation.IsServer = true;
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "maybe" })).Succeeded);
            Assert.True(ModConfigProvider.ModOptions.VoiceEnabled);
            Assert.True(command.ProcessCommand(args.FromValues(new[] { "false" })).Succeeded);
            Assert.False(ModConfigProvider.ModOptions.VoiceEnabled);
            broker.Verify(x => x.Publish(It.IsAny<object>(), It.Is<ModConfigApplied>(m => !m.ModOptions.VoiceEnabled)), Times.Once);
            network.Verify(x => x.SendAll(It.Is<NetworkLoadModConfig>(m => !m.ModOptions.VoiceEnabled)), Times.Once);
            ModInformation.IsServer = false;
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "true" })).Succeeded);
            Assert.False(ModConfigProvider.ModOptions.VoiceEnabled);
            ModInformation.IsServer = true;
            Assert.True(command.ProcessCommand(args.FromValues(new[] { "true" })).Succeeded);
            Assert.True(ModConfigProvider.ModOptions.VoiceEnabled);
        }
        finally
        {
            ModInformation.IsServer = server;
            ModConfigProvider.ModOptions = previous;
        }
    }
}
