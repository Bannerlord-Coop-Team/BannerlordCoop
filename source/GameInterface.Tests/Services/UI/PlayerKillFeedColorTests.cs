using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI;
using GameInterface.Services.UI.Handlers;
using GameInterface.Services.UI.Messages;
using Moq;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace GameInterface.Tests.Services.UI;

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
    public void OptionsStore_SaveAndReload_RoundTripsColor()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var store = new PlayerKillFeedColorOptionsStore(filePath);
            var expected = new PlayerKillFeedColor(12, 34, 56);

            store.Save(expected);

            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            Assert.True(document.RootElement.TryGetProperty("killFeedColor", out var killFeedColor));
            Assert.False(document.RootElement.TryGetProperty("Red", out _));
            Assert.Equal(expected.Red, killFeedColor.GetProperty("Red").GetInt32());
            Assert.Equal(expected.Green, killFeedColor.GetProperty("Green").GetInt32());
            Assert.Equal(expected.Blue, killFeedColor.GetProperty("Blue").GetInt32());

            var reloaded = new PlayerKillFeedColorOptionsStore(filePath);
            Assert.True(reloaded.TryLoad(out var actual));
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
    public void OptionsStore_TryLoad_RejectsLegacyFlatColor()
    {
        var filePath = CreateTempFilePath();

        try
        {
            File.WriteAllText(filePath, "{\"Red\":170,\"Green\":170,\"Blue\":170}");
            var store = new PlayerKillFeedColorOptionsStore(filePath);

            Assert.False(store.TryLoad(out _));
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
    public void CoopOptionsVM_ClampsRgbAndUpdatesPreview()
    {
        var filePath = CreateTempFilePath();
        var viewModel = new CoopOptionsVM(new PlayerKillFeedColorOptionsStore(filePath), new MessageBroker());

        viewModel.Red = 999;
        viewModel.Green = -20;
        viewModel.Blue = 16;

        Assert.Equal(255, viewModel.Red);
        Assert.Equal(0, viewModel.Green);
        Assert.Equal(16, viewModel.Blue);
        Assert.Equal("#FF0010FF", viewModel.PreviewColorString);
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

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"BannerlordCoop-{Guid.NewGuid():N}.json");
    }

    private class TestOptionsStore : IPlayerKillFeedColorOptionsStore
    {
        public string FilePath => string.Empty;
        public PlayerKillFeedColor? SavedColor { get; private set; }

        public bool TryLoad(out PlayerKillFeedColor color)
        {
            if (SavedColor.HasValue)
            {
                color = SavedColor.Value;
                return true;
            }

            color = default;
            return false;
        }

        public PlayerKillFeedColor LoadOrDefault()
        {
            return SavedColor ?? new PlayerKillFeedColor(59, 130, 246);
        }

        public void Save(PlayerKillFeedColor color)
        {
            SavedColor = color;
        }
    }
}
