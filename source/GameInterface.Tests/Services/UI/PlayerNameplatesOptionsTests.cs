using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.PlayerNameplates;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Verifies the client and server nameplate option gates.</summary>
[Collection("ModConfigSerial")]
public class PlayerNameplatesOptionsTests
{
    [Fact]
    public void PlayerNameplates_DefaultToEnabled()
    {
        using var messageBroker = new MessageBroker();
        var provider = new PlayerNameplatesOptionsTabProvider();

        var tab = provider.CreateTab(new CoopOptionsData(), messageBroker, _ => { });

        var section = Assert.IsType<PlayerNameplatesSection>(Assert.Single(tab.Sections));
        Assert.True(section.ShowPlayerNameplates);
    }

    [Fact]
    public void PlayerNameplates_Disabled_PersistAndPublishAfterApply()
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

            section.ShowPlayerNameplates = false;
            var options = store.LoadOrDefault();
            tab.Apply(options);
            store.Save(options);
            tab.AfterApply();

            Assert.False(PlayerNameplatesOptionsTabProvider.GetShowPlayerNameplatesOrDefault(store.LoadOrDefault()));
            Assert.True(selected.HasValue);
            Assert.False(selected.Value.ShowPlayerNameplates);
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
        var original = ModConfigProvider.ModOptions;
        string filePath = CreateTempFilePath();

        try
        {
            ModConfigProvider.LoadModConfig(new ModOptionsData { ShowPlayerNameplates = false });
            using var messageBroker = new MessageBroker();
            var viewModel = new CoopOptionsVM(new CoopOptionsStore(filePath), messageBroker);

            Assert.Null(viewModel.PlayerNameplatesTab);
            Assert.DoesNotContain(viewModel.Tabs, tab => tab.Id == PlayerNameplatesOptionsTabProvider.TabId);
        }
        finally
        {
            ModConfigProvider.ModOptions = original;
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
            new PlayerNameplateEligibility().IsAlliedTeam(isPlayerTeam, isEnemyOfPlayerTeam));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"bannerlord-coop-nameplate-options-{Guid.NewGuid():N}.json");
    }
}
