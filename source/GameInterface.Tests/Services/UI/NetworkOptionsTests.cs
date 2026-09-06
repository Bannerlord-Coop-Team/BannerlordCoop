using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab.Sections;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(nameof(CoopOptionsViewModelCollection))]
public class NetworkOptionsTests
{
    [Fact]
    public void CoopOptionsVM_NetworkBandwidthDefaultsToFiveMiBPerSecond()
    {
        string filePath = CreateTempFilePath();

        try
        {
            var viewModel = CoopOptionsVMTestFactory.Create(
                new CoopOptionsStore(filePath),
                new MessageBroker());
            var tab = Assert.Single(viewModel.Tabs.Where(value =>
                value.Id == NetworkOptionsTabProvider.TabId));
            var section = Assert.IsType<NetworkSection>(Assert.Single(tab.Sections));

            Assert.Equal(NetworkOptionsTabProvider.TabName, tab.Name);
            Assert.Equal(5f, section.MovementUploadMiBPerSecond);
            Assert.Equal(5f, section.MovementDownloadMiBPerSecond);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void CoopOptionsVM_NetworkBandwidthPersistsAfterApply()
    {
        string filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            var viewModel = CoopOptionsVMTestFactory.Create(store, new MessageBroker());
            var tab = Assert.Single(viewModel.Tabs.Where(value =>
                value.Id == NetworkOptionsTabProvider.TabId));
            var section = Assert.IsType<NetworkSection>(Assert.Single(tab.Sections));

            tab.ExecuteSelection();
            section.MovementUploadMiBPerSecond = 7.5f;
            section.MovementDownloadMiBPerSecond = 9.25f;
            viewModel.ActionApply();

            var bandwidth = new LocalMovementBandwidth(store);
            Assert.Equal(7.5d, bandwidth.UploadMiBPerSecond);
            Assert.Equal(9.25d, bandwidth.DownloadMiBPerSecond);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void LocalMovementBandwidth_InvalidSavedValuesAreIgnored()
    {
        string filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            var options = new CoopOptionsData();
            options.SetSection(
                NetworkOptionsTabProvider.TabId,
                NetworkSection.SectionId,
                new NetworkSectionOptions
                {
                    MovementUploadMiBPerSecond = -1d,
                    MovementDownloadMiBPerSecond = 2048d,
                });
            store.Save(options);

            var bandwidth = new LocalMovementBandwidth(store);
            Assert.Null(bandwidth.UploadMiBPerSecond);
            Assert.Null(bandwidth.DownloadMiBPerSecond);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"bannerlord-coop-network-options-{Guid.NewGuid():N}.json");
    }
}
