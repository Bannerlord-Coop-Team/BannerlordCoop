using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab.Sections;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class MapTimeOptionsTests
{
    [Fact]
    public void CoopOptionsVM_MapTimeDefaultsToEnabled()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var viewModel = new CoopOptionsVM(new CoopOptionsStore(filePath), new MessageBroker());
            var tab = viewModel.Tabs[1];

            Assert.Equal(MapTimeOptionsTabProvider.TabName, tab.Name);
            Assert.Equal(MapTimeOptionsTabProvider.TabId, tab.Id);
            
            var section = Assert.IsType<MapTimeSection>(Assert.Single(tab.Sections));
            Assert.Equal(MapTimeSection.SectionId, section.Id);
            Assert.True(section.ShowMapTimeInMissions);
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
    public void CoopOptionsVM_MapTimeDisabled_PersistsAfterApply()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            var viewModel = new CoopOptionsVM(store, new MessageBroker());
            var tab = viewModel.Tabs[1];
            var section = Assert.IsType<MapTimeSection>(Assert.Single(tab.Sections));

            tab.ExecuteSelection();
            section.ShowMapTimeInMissions = false;
            viewModel.ActionApply();

            var options = store.LoadOrDefault();

            Assert.False(MapTimeOptionsTabProvider.GetShowMapTimeInMissionsOrDefault(options));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(),
            $"bannerlord-coop-map-time-options-{Guid.NewGuid():N}.json");
    }
}