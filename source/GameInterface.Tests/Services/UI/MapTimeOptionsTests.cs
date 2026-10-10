using GameInterface.Services.UI.CoopOptions.Providers.UITab;
using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab.Sections;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(nameof(CoopOptionsViewModelCollection))]
public class MapTimeOptionsTests
{
    [Fact]
    public void CoopOptionsVM_MapTimeDefaultsToEnabled()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var viewModel = CoopOptionsVMTestFactory.Create(new CoopOptionsStore(filePath), new MessageBroker());
            var tab = viewModel.UITab;

            Assert.Equal("UI", tab.Name);
            Assert.Equal(UIOptionsTabProvider.TabId, tab.Id);
            
            var section = Assert.IsType<UISection>(Assert.Single(tab.Sections)).MapTime;
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
            var viewModel = CoopOptionsVMTestFactory.Create(store, new MessageBroker());
            var tab = viewModel.UITab;
            var section = Assert.IsType<UISection>(Assert.Single(tab.Sections)).MapTime;

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
