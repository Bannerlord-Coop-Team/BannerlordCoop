using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab.Sections;
using GameInterface.Services.UI.Handlers;
using GameInterface.Services.UI.Messages;
using GameInterface.Tests;
using Moq;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class PlayerKillFeedColorTests
{
    [Fact]
    public void TryCreate_InvalidComponents_ReturnsFalse()
    {
        Assert.False(PlayerKillFeedColor.TryCreate(-1, 20, 30, out _));
        Assert.False(PlayerKillFeedColor.TryCreate(10, 256, 30, out _));
        Assert.False(PlayerKillFeedColor.TryCreate(10, 20, 300, out _));
    }

    [Fact]
    public void TryParseHex_ParsesRgbAndIgnoresAlpha()
    {
        Assert.True(PlayerKillFeedColor.TryParseHex("#0A141EFF", out var color));

        Assert.Equal(new PlayerKillFeedColor(10, 20, 30), color);
        Assert.Equal("#0A141E", color.ToHex());
        Assert.Equal("#0A141EFF", color.ToColorString());
    }

    [Fact]
    public void CoopOptionsStore_SaveAndReload_RoundTripsColor()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            var expected = new PlayerKillFeedColor(12, 34, 56);
            var options = CreateOptions(expected);

            store.Save(options);

            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            Assert.True(document.RootElement.TryGetProperty(KillFeedOptionsTabProvider.TabId, out var killFeedTab));
            Assert.False(document.RootElement.TryGetProperty("killFeedColor", out _));
            Assert.True(killFeedTab.TryGetProperty(KillFeedSection.SectionId, out var killFeedSection));
            Assert.True(killFeedSection.TryGetProperty("killFeedColor", out var killFeedColor));
            Assert.Equal(expected.Red, killFeedColor.GetProperty("Red").GetInt32());
            Assert.Equal(expected.Green, killFeedColor.GetProperty("Green").GetInt32());
            Assert.Equal(expected.Blue, killFeedColor.GetProperty("Blue").GetInt32());

            var reloaded = new CoopOptionsStore(filePath);
            Assert.True(reloaded.TryLoad(out var actualOptions));
            Assert.True(KillFeedOptionsTabProvider.TryGetKillFeedColor(actualOptions, out var actual));
            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void CoopOptionsVM_ClampsKillFeedColorAndUpdatesPreview()
    {
        var filePath = CreateTempFilePath();
        var viewModel = new CoopOptionsVM(new CoopOptionsStore(filePath), new MessageBroker());
        var section = GetKillFeedSection(viewModel);

        section.KillFeedColorRed = 999;
        section.KillFeedColorGreen = -20;
        section.KillFeedColorBlue = 16;

        Assert.Equal(255, section.KillFeedColorRed);
        Assert.Equal(0, section.KillFeedColorGreen);
        Assert.Equal(16, section.KillFeedColorBlue);
        Assert.Equal("#FF0010FF", section.KillFeedPreviewColorString);
    }

    [Fact]
    public void CoopOptionsVM_DefaultTabs_SelectsKillFeedColor()
    {
        var filePath = CreateTempFilePath();
        var viewModel = new CoopOptionsVM(new CoopOptionsStore(filePath), new MessageBroker());

        var tab = Assert.Single(viewModel.Tabs);
        Assert.Equal(KillFeedOptionsTabProvider.TabName, tab.Name);
        Assert.Equal(KillFeedOptionsTabProvider.TabId, tab.Id);
        Assert.Same(tab, viewModel.SelectedTab);
        Assert.True(tab.IsSelected);
        Assert.True(viewModel.IsApplyButtonVisible);
        var section = Assert.IsType<KillFeedSection>(Assert.Single(tab.Sections));
        Assert.Equal(KillFeedSection.SectionId, section.Id);
    }

    [Fact]
    public void CoopOptionsVM_ActionCancel_UsesHostCloseAction()
    {
        var closeCalled = false;
        var viewModel = new CoopOptionsVM(
            new CoopOptionsStore(CreateTempFilePath()),
            new MessageBroker(),
            () => closeCalled = true);

        viewModel.ActionCancel();

        Assert.True(closeCalled);
    }

    [Fact]
    public void Handler_LocalSelection_CachesAndSendsRequest()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var broker = new MessageBroker();
            var network = new Mock<INetwork>();
            var playerManager = new Mock<IPlayerManager>();
            var colorService = new PlayerKillFeedColorService();
            var optionsStore = new TestOptionsStore();
            var controllerIdProvider = new ControllerIdProvider();
            controllerIdProvider.SetControllerId("PlayerOne");

            IMessage sentMessage = null!;
            network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message => sentMessage = message);

            using var handler = new PlayerKillFeedColorHandler(
                broker,
                network.Object,
                playerManager.Object,
                colorService,
                optionsStore,
                controllerIdProvider);

            var color = new PlayerKillFeedColor(11, 22, 33);
            broker.Publish(this, new PlayerKillFeedColorSelected(color));

            Assert.True(colorService.TryGetColor("PlayerOne", out var cachedColor));
            Assert.Equal(color, cachedColor);

            var request = Assert.IsType<NetworkRequestKillFeedColor>(sentMessage);
            Assert.Equal(color.Red, request.Red);
            Assert.Equal(color.Green, request.Green);
            Assert.Equal(color.Blue, request.Blue);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handler_ResendRequested_ReadsNestedSavedColorAndSendsRequest()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var broker = new MessageBroker();
            var network = new Mock<INetwork>();
            var playerManager = new Mock<IPlayerManager>();
            var colorService = new PlayerKillFeedColorService();
            var optionsStore = new TestOptionsStore();
            var controllerIdProvider = new ControllerIdProvider();
            controllerIdProvider.SetControllerId("PlayerOne");

            var color = new PlayerKillFeedColor(44, 55, 66);
            optionsStore.Save(CreateOptions(color));

            IMessage sentMessage = null!;
            network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message => sentMessage = message);

            using var handler = new PlayerKillFeedColorHandler(
                broker,
                network.Object,
                playerManager.Object,
                colorService,
                optionsStore,
                controllerIdProvider);

            broker.Publish(this, new PlayerKillFeedColorResendRequested());

            Assert.True(colorService.TryGetColor("PlayerOne", out var cachedColor));
            Assert.Equal(color, cachedColor);

            var request = Assert.IsType<NetworkRequestKillFeedColor>(sentMessage);
            Assert.Equal(color.Red, request.Red);
            Assert.Equal(color.Green, request.Green);
            Assert.Equal(color.Blue, request.Blue);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    private static KillFeedSection GetKillFeedSection(CoopOptionsVM viewModel)
    {
        var tab = Assert.Single(viewModel.Tabs);
        return Assert.IsType<KillFeedSection>(Assert.Single(tab.Sections));
    }

    private static CoopOptionsData CreateOptions(PlayerKillFeedColor color)
    {
        var options = new CoopOptionsData();
        options.SetSection(KillFeedOptionsTabProvider.TabId, KillFeedSection.SectionId, KillFeedSectionOptions.FromColor(color));
        return options;
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"BannerlordCoop-{Guid.NewGuid():N}.json");
    }

    private class TestOptionsStore : ICoopOptionsStore
    {
        public string FilePath => string.Empty;
        public CoopOptionsData? SavedOptions { get; private set; }

        public bool TryLoad(out CoopOptionsData options)
        {
            if (SavedOptions != null)
            {
                options = SavedOptions;
                return true;
            }

            options = null!;
            return false;
        }

        public CoopOptionsData LoadOrDefault()
        {
            return SavedOptions ?? new CoopOptionsData();
        }

        public void Save(CoopOptionsData options)
        {
            SavedOptions = options;
        }
    }
}
