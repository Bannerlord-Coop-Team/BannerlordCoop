using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions.Sections;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.Patches;
using GameInterface.Tests;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using TaleWorlds.Engine.Options;
using Xunit;
using ManagedOptionsType = TaleWorlds.MountAndBlade.ManagedOptions.ManagedOptionsType;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class ServerBattleSizeTests
{
    [Fact]
    public void Provider_DefaultsToOneThousandAndClampsUpdates()
    {
        var provider = new ServerBattleSizeProvider();

        Assert.Equal(1000, provider.BattleSize);

        provider.SetBattleSize(1200);
        Assert.Equal(ServerBattleSizeProvider.MaximumBattleSize, provider.BattleSize);

        provider.SetBattleSize(100);
        Assert.Equal(ServerBattleSizeProvider.MinimumBattleSize, provider.BattleSize);

        provider.SetBattleSize(650);
        Assert.Equal(650, provider.BattleSize);
    }

    [Fact]
    public void CoopOptionsStore_SaveAndReload_RoundTripsServerBattleSize()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var options = new CoopOptionsData();
            options.SetSection(
                ServerOptionsTabProvider.TabId,
                BattleSizeSection.SectionId,
                BattleSizeSectionOptions.FromBattleSize(600));

            new CoopOptionsStore(filePath).Save(options);

            Assert.True(new CoopOptionsStore(filePath).TryLoad(out var reloaded));
            Assert.True(ServerOptionsTabProvider.TryGetBattleSize(reloaded, out var battleSize));
            Assert.Equal(600, battleSize);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Theory]
    [InlineData(650)]
    [InlineData(1200)]
    public void UnsupportedSavedBattleSize_UsesDefault(int unsupportedBattleSize)
    {
        var options = new CoopOptionsData();
        options.SetSection(
            ServerOptionsTabProvider.TabId,
            BattleSizeSection.SectionId,
            new BattleSizeSectionOptions { BattleSize = unsupportedBattleSize });

        Assert.False(ServerOptionsTabProvider.TryGetBattleSize(options, out _));
        Assert.Equal(
            ServerBattleSizeProvider.DefaultBattleSize,
            ServerOptionsTabProvider.GetBattleSizeOrDefault(options));
    }

    [Fact]
    public void BattleSizeSectionOptions_NormalizesToNearestSliderValue()
    {
        var options = BattleSizeSectionOptions.FromBattleSize(650);

        Assert.Equal(600, options.BattleSize);
        Assert.True(options.TryGetBattleSize(out var battleSize));
        Assert.Equal(600, battleSize);
    }

    [Fact]
    public void CoopOptionsVM_ServerRole_ShowsOnlyServerOptions()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var viewModel = new CoopOptionsVM(
                new CoopOptionsStore(CreateTempFilePath()),
                new MessageBroker(),
                () => { });

            Assert.Equal("Server Options", viewModel.MovieTextHeader);
            var tab = Assert.Single(viewModel.Tabs);
            Assert.Equal(ServerOptionsTabProvider.TabId, tab.Id);
            Assert.Same(tab, viewModel.SelectedServerOptionsTab);
            Assert.Null(viewModel.SelectedKillFeedTab);
            Assert.True(viewModel.IsServerOptionsVisible);
            Assert.False(viewModel.IsKillFeedOptionsVisible);

            var section = Assert.IsType<BattleSizeSection>(Assert.Single(tab.Sections));
            Assert.Equal(1000, section.BattleSize);
            Assert.Equal("1000", section.BattleSizeText);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void CoopOptionsVM_ClientRole_ShowsOnlyCoopOptions()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var viewModel = new CoopOptionsVM(
                new CoopOptionsStore(CreateTempFilePath()),
                new MessageBroker(),
                () => { });

            Assert.Equal("Coop Options", viewModel.MovieTextHeader);
            var tab = Assert.Single(viewModel.Tabs);
            Assert.Equal(KillFeedOptionsTabProvider.TabId, tab.Id);
            Assert.Same(tab, viewModel.SelectedKillFeedTab);
            Assert.Null(viewModel.SelectedServerOptionsTab);
            Assert.True(viewModel.IsKillFeedOptionsVisible);
            Assert.False(viewModel.IsServerOptionsVisible);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void BattleSizeSection_AfterApplyPublishesActualBattleSize()
    {
        using var broker = new MessageBroker();
        ServerBattleSizeSelected? selected = null;
        Action<MessagePayload<ServerBattleSizeSelected>> subscription = payload => selected = payload.What;
        broker.Subscribe(subscription);
        var section = new BattleSizeSection(1000, broker)
        {
            BattleSizeIndex = 3
        };
        var options = new CoopOptionsData();

        section.Apply(ServerOptionsTabProvider.TabId, options);
        section.AfterApply();

        Assert.True(ServerOptionsTabProvider.TryGetBattleSize(options, out var savedBattleSize));
        Assert.Equal(500, savedBattleSize);
        Assert.True(selected.HasValue);
        Assert.Equal(500, selected.Value.BattleSize);
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(1, 300)]
    [InlineData(2, 400)]
    [InlineData(3, 500)]
    [InlineData(4, 600)]
    [InlineData(5, 800)]
    [InlineData(6, 1000)]
    public void SliderIndex_UsesVanillaFieldBattleSizeValues(int index, int expectedBattleSize)
    {
        Assert.Equal(expectedBattleSize, ServerOptionsTabProvider.GetBattleSizeForIndex(index));
    }

    [Fact]
    public void CoopOptionsMovie_ScopesProviderLayoutsAndUsesBattleSizeSlider()
    {
        var document = XDocument.Load(FindMoviePath());

        var killFeedLayout = Assert.Single(document.Descendants("ListPanel"),
            element => element.Attribute("IsVisible")?.Value == "@IsKillFeedOptionsVisible");
        var serverOptionsLayout = Assert.Single(document.Descendants("ListPanel"),
            element => element.Attribute("IsVisible")?.Value == "@IsServerOptionsVisible");
        Assert.Single(killFeedLayout.Descendants("ListPanel"),
            element => element.Attribute("DataSource")?.Value == "{SelectedKillFeedTab}");
        Assert.Single(serverOptionsLayout.Descendants("ListPanel"),
            element => element.Attribute("DataSource")?.Value == "{SelectedServerOptionsTab}");

        var slider = Assert.Single(document.Descendants("Standard.Slider"),
            element => element.Attribute("Parameter.ValueInt")?.Value == "@BattleSizeIndex");
        Assert.Equal("0", slider.Attribute("Parameter.MinValue")?.Value);
        Assert.Equal("6", slider.Attribute("Parameter.MaxValue")?.Value);
    }

    [Fact]
    public void VanillaOptionsFilter_RemovesOnlyBattleSize()
    {
        var battleSizeOption = new Mock<IOptionData>();
        battleSizeOption.Setup(option => option.GetOptionType()).Returns(ManagedOptionsType.BattleSize);
        var otherOption = new Mock<IOptionData>();
        otherOption.Setup(option => option.GetOptionType()).Returns(ManagedOptionsType.NumberOfCorpses);

        var filtered = RemoveVanillaBattleSizeOptionPatch.FilterBattleSizeOption(
            new[] { battleSizeOption.Object, otherOption.Object }).ToArray();

        Assert.Equal(otherOption.Object, Assert.Single(filtered));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"BannerlordCoop-{Guid.NewGuid():N}.json");
    }

    private static string FindMoviePath([CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", "CoopOptionsUIMovie.xml"));
    }
}
