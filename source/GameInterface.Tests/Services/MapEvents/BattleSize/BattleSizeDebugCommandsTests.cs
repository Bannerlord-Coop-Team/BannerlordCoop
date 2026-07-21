using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.MapEvents.BattleSize.Commands;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Tests;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.BattleSize;

[Collection(ModInformationRoleCollection.Name)]
public class BattleSizeDebugCommandsTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private ILifetimeScope container = null!;

    public void Dispose()
    {
        ContainerProvider.Clear();
        container?.Dispose();
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void Status_ReturnsCurrentRoleAndBattleSize()
    {
        ModInformation.IsServer = false;
        var provider = new ServerBattleSizeProvider();
        provider.SetBattleSize(300);
        SetContainer(provider, new MessageBroker());

        Assert.Equal(
            "Battle size (client): 300.",
            BattleSizeDebugCommands.Status(new List<string>()));
    }

    [Fact]
    public void SetRuntime_WhenClient_ReturnsServerOnlyError()
    {
        ModInformation.IsServer = false;

        Assert.Equal(
            "Run this command on the server.",
            BattleSizeDebugCommands.SetRuntime(new List<string> { "300" }));
    }

    [Theory]
    [InlineData("199")]
    [InlineData("700")]
    [InlineData("invalid")]
    public void SetRuntime_WhenValueIsUnsupported_ReturnsUsage(string value)
    {
        ModInformation.IsServer = true;

        Assert.StartsWith(
            "Usage:",
            BattleSizeDebugCommands.SetRuntime(new List<string> { value }));
    }

    [Fact]
    public void SetRuntime_WhenServer_UsesTheExistingSyncPath()
    {
        ModInformation.IsServer = true;
        var provider = new ServerBattleSizeProvider();
        var broker = new MessageBroker();
        var network = new Mock<INetwork>();
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(value => value.LoadOrDefault()).Returns(new CoopOptionsData());
        IMessage sent = null!;
        network.Setup(value => value.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sent = message);
        using var handler = new ServerBattleSizeSyncHandler(
            broker,
            network.Object,
            store.Object,
            provider);
        SetContainer(provider, broker);

        var result = BattleSizeDebugCommands.SetRuntime(new List<string> { "300" });

        Assert.Equal("Server battle size set to 300 for this runtime.", result);
        Assert.Equal(300, provider.BattleSize);
        Assert.Equal(300, Assert.IsType<NetworkBattleSizeChanged>(sent).BattleSize);
    }

    private void SetContainer(
        IServerBattleSizeProvider provider,
        IMessageBroker messageBroker)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(provider).As<IServerBattleSizeProvider>();
        builder.RegisterInstance(messageBroker).As<IMessageBroker>();
        container = builder.Build();
        ContainerProvider.SetContainer(container);
    }
}
