using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Coop.Tests.Mocks;
using GameInterface.Services.UI;
using GameInterface.Services.UI.Commands;
using GameInterface.Services.UI.Handlers;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using GameInterface.Tests;
using Moq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class TacticalUnitSymbolsConfigTests
{
    static TacticalUnitSymbolsConfigTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void ServerConsoleCommand_WhenClient_ReturnsServerOnlyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var result = TacticalUnitSymbolsDebugCommand.TacticalSymbols(new List<string> { "on" });

            Assert.Equal(
                "The 'coop.debug.ui.tactical_symbols' command cannot be used on the client. It is intended for server use only.",
                result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ServerConsoleCommand_Status_ReturnsCurrentSetting()
    {
        var wasServer = ModInformation.IsServer;
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        ModInformation.IsServer = true;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(true);

        try
        {
            var result = TacticalUnitSymbolsDebugCommand.TacticalSymbols(new List<string> { "status" });

            Assert.Equal("Tactical unit symbols are hidden.", result);
        }
        finally
        {
            TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(previous);
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ServerConsoleCommand_On_ShowsAndBroadcastsTheAuthoritativeSetting()
    {
        var wasServer = ModInformation.IsServer;
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        ModInformation.IsServer = true;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(true);

        try
        {
            var broker = new MessageBroker();
            var network = new Mock<INetwork>();
            IMessage sent = null!;
            network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message => sent = message);

            var builder = new ContainerBuilder();
            builder.RegisterInstance(broker).As<IMessageBroker>();
            builder.RegisterInstance(network.Object).As<INetwork>();
            builder.RegisterType<TacticalUnitSymbolsConfigHandler>().AsSelf();
            using var container = builder.Build();
            ContainerProvider.SetContainer(container);

            var result = TacticalUnitSymbolsDebugCommand.TacticalSymbols(new List<string> { "on" });

            Assert.Equal("Tactical unit symbols are visible.", result);
            Assert.False(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols);
            var changed = Assert.IsType<NetworkTacticalUnitSymbolsVisibilityChanged>(sent);
            Assert.False(changed.HideTacticalUnitSymbols);
        }
        finally
        {
            ContainerProvider.Clear();
            TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(previous);
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handler_ClientBroadcast_UpdatesTheSynchronizedSetting()
    {
        var wasServer = ModInformation.IsServer;
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        ModInformation.IsServer = false;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(false);

        try
        {
            var broker = new MessageBroker();
            using var handler = new TacticalUnitSymbolsConfigHandler(broker, Mock.Of<INetwork>());

            broker.Publish(this, new NetworkTacticalUnitSymbolsVisibilityChanged(true));

            Assert.True(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols);
        }
        finally
        {
            TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(previous);
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ConfigSnapshot_SendsCurrentSettingToJoiningPeer()
    {
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(true);

        try
        {
            var network = new TestNetwork();
            var peer = network.CreatePeer();
            var config = new TacticalUnitSymbolsConfigInterface(network);

            config.SendSnapshot(peer);

            var changed = Assert.Single(network.GetPeerMessagesFromType<NetworkTacticalUnitSymbolsVisibilityChanged>(peer));
            Assert.True(changed.HideTacticalUnitSymbols);
        }
        finally
        {
            TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(previous);
        }
    }
}
