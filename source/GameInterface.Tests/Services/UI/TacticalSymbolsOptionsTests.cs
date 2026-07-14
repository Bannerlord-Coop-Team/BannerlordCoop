using Common;
using Common.Messaging;
using Common.Network;
using Coop.Tests.Mocks;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab;
using GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab.Sections;
using GameInterface.Services.UI.Handlers;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using GameInterface.Tests;
using Moq;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class TacticalSymbolsOptionsTests
{
    static TacticalSymbolsOptionsTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void TacticalSymbolsSection_DoesNotPersistTheSessionSetting()
    {
        var section = new TacticalSymbolsSection(false, new MessageBroker());
        var options = new CoopOptionsData();

        section.ExecuteToggleHideTacticalUnitSymbols();
        section.Apply(TacticalSymbolsOptionsTabProvider.TabId, options);

        Assert.True(section.HideTacticalUnitSymbols);
        Assert.False(options.Tabs.ContainsKey(TacticalSymbolsOptionsTabProvider.TabId));
    }

    [Fact]
    public void TacticalSymbolsTab_UsesTheSynchronizedSetting()
    {
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(true);

        try
        {
            var provider = new TacticalSymbolsOptionsTabProvider();
            var tab = provider.CreateTab(new CoopOptionsData(), new MessageBroker(), _ => { });
            var section = Assert.IsType<TacticalSymbolsSection>(Assert.Single(tab.Sections));

            Assert.True(section.HideTacticalUnitSymbols);
            Assert.False(tab.PersistsOptions);
        }
        finally
        {
            TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(previous);
        }
    }

    [Fact]
    public void Handler_ClientSelection_RequestsServerUpdateWithoutChangingLocalSetting()
    {
        var wasServer = ModInformation.IsServer;
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        ModInformation.IsServer = false;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(false);

        try
        {
            var broker = new MessageBroker();
            var network = new Mock<INetwork>();
            IMessage sent = null!;
            network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message => sent = message);

            using var handler = new TacticalUnitSymbolsOptionsHandler(broker, network.Object);

            broker.Publish(this, new TacticalUnitSymbolsVisibilitySelected(true));

            Assert.False(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols);
            var request = Assert.IsType<NetworkRequestTacticalUnitSymbolsVisibilityChange>(sent);
            Assert.True(request.HideTacticalUnitSymbols);
        }
        finally
        {
            TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(previous);
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handler_ServerRequest_UpdatesAndBroadcastsTheAuthoritativeSetting()
    {
        var wasServer = ModInformation.IsServer;
        var previous = TacticalUnitSymbolsSettings.HideTacticalUnitSymbols;
        ModInformation.IsServer = true;
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(false);

        try
        {
            var broker = new MessageBroker();
            var network = new Mock<INetwork>();
            IMessage sent = null!;
            network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message => sent = message);

            using var handler = new TacticalUnitSymbolsOptionsHandler(broker, network.Object);

            broker.Publish(this, new NetworkRequestTacticalUnitSymbolsVisibilityChange(true));

            Assert.True(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols);
            var changed = Assert.IsType<NetworkTacticalUnitSymbolsVisibilityChanged>(sent);
            Assert.True(changed.HideTacticalUnitSymbols);
        }
        finally
        {
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
            using var handler = new TacticalUnitSymbolsOptionsHandler(broker, Mock.Of<INetwork>());

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
