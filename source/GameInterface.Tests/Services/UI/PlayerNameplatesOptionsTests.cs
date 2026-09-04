using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.PlayerNameplates;
using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Verifies the client and server nameplate option gates.</summary>
[Collection("ModConfigSerial")]
public class PlayerNameplatesOptionsTests
{
    [Fact]
    public void PlayerNameplates_DefaultToAlways()
    {
        using var messageBroker = new MessageBroker();
        var provider = new PlayerNameplatesOptionsTabProvider();

        var tab = provider.CreateTab(new CoopOptionsData(), messageBroker, _ => { });

        var section = Assert.IsType<PlayerNameplatesSection>(Assert.Single(tab.Sections));
        Assert.Equal(PlayerNameplatesDisplayMode.Always, section.SelectedDisplayMode);
    }

    [Theory]
    [InlineData(PlayerNameplatesDisplayMode.Always)]
    [InlineData(PlayerNameplatesDisplayMode.HoldIndicators)]
    [InlineData(PlayerNameplatesDisplayMode.Never)]
    public void PlayerNameplates_Mode_PersistAndPublishAfterApply(PlayerNameplatesDisplayMode mode)
    {
        string filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            using var messageBroker = new MessageBroker();
            PlayerNameplateVisibilitySelected? selected = null;
            Action<MessagePayload<PlayerNameplateVisibilitySelected>> handler = payload => selected = payload.What;
            messageBroker.Subscribe(handler);
            var provider = new PlayerNameplatesOptionsTabProvider();
            var tab = provider.CreateTab(store.LoadOrDefault(), messageBroker, _ => { });
            var section = Assert.IsType<PlayerNameplatesSection>(Assert.Single(tab.Sections));

            section.DisplayModeSelector.SelectedIndex = (int)mode;
            var options = store.LoadOrDefault();
            tab.Apply(options);
            store.Save(options);
            tab.AfterApply();

            Assert.Equal(mode, PlayerNameplatesOptionsTabProvider.GetDisplayModeOrDefault(store.LoadOrDefault()));
            Assert.True(selected.HasValue);
            Assert.Equal(mode, selected.Value.DisplayMode);
            GC.KeepAlive(handler);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void ServerDisabled_HidesPlayerNameplatesTab()
    {
        string filePath = CreateTempFilePath();

        try
        {
            using var messageBroker = new MessageBroker();
            var viewModel = CreateViewModel(filePath, messageBroker, false);

            Assert.Null(viewModel.PlayerNameplatesTab);
            Assert.DoesNotContain(viewModel.Tabs, tab => tab.Id == PlayerNameplatesOptionsTabProvider.TabId);
            viewModel.OnFinalize();
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void ServerDisabledAfterMenuOpened_RemovesPlayerNameplatesTab()
    {
        string filePath = CreateTempFilePath();

        try
        {
            using var messageBroker = new MessageBroker();
            var viewModel = CreateViewModel(filePath, messageBroker, true);
            Assert.NotNull(viewModel.PlayerNameplatesTab);

            messageBroker.Publish(
                this,
                new ModConfigApplied(new ModOptions(new ModOptionsData { ShowPlayerNameplates = false })));

            Assert.Null(viewModel.PlayerNameplatesTab);
            Assert.DoesNotContain(viewModel.Tabs, tab => tab.Id == PlayerNameplatesOptionsTabProvider.TabId);
            Assert.Equal(KillFeedOptionsTabProvider.TabId, viewModel.SelectedTab.Id);
            viewModel.OnFinalize();
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public void AlliedTeam_UsesMissionTeamRelationship(
        bool isPlayerTeam,
        bool isEnemyOfPlayerTeam,
        bool expected)
    {
        Assert.Equal(
            expected,
            new PlayerNameplateEligibility().IsAlliedTeam(
                isPlayerTeam,
                isEnemyOfPlayerTeam,
                false,
                false,
                false));
    }

    [Fact]
    public void InvalidTournamentSpectatorTeams_AreAllied()
    {
        Assert.True(new PlayerNameplateEligibility().IsAlliedTeam(false, false, true, false, true));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void MixedTeamValidity_IsAlliedOnlyInTournaments(bool isTournament, bool expected)
    {
        Assert.Equal(
            expected,
            new PlayerNameplateEligibility().IsAlliedTeam(false, false, false, true, isTournament));
    }

    [Fact]
    public void PlayerNameplatesMovie_ReferencesNestedMarkerWidgetsByPath()
    {
        var document = XDocument.Load(PopupUIMovieBindingTests.FindMoviePath("PlayerNameplates.xml"));
        var marker = Assert.Single(document.Descendants("NameMarkerListPanel"));

        Assert.Equal(@"NameContainer\NameText", marker.Attribute("NameTextWidget")?.Value);
        Assert.Equal(@"NameContainer\TypeVisual", marker.Attribute("TypeVisualWidget")?.Value);

        var container = Assert.Single(marker.Descendants(),
            element => element.Attribute("Id")?.Value == "NameContainer");
        Assert.Single(container.Descendants(), element => element.Attribute("Id")?.Value == "NameText");
        Assert.Single(container.Descendants(), element => element.Attribute("Id")?.Value == "TypeVisual");
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"bannerlord-coop-nameplate-options-{Guid.NewGuid():N}.json");
    }

    private static CoopOptionsVM CreateViewModel(
        string filePath,
        IMessageBroker messageBroker,
        bool serverAllowsNameplates)
    {
        ICoopOptionsTabProvider[] providers =
        {
            new KillFeedOptionsTabProvider(),
            new PlayerNameplatesOptionsTabProvider()
        };
        var modOptions = new ModOptions(new ModOptionsData
        {
            ShowPlayerNameplates = serverAllowsNameplates
        });
        return new CoopOptionsVM(
            new CoopOptionsStore(filePath),
            messageBroker,
            providers,
            modOptions,
            () => { });
    }
}
