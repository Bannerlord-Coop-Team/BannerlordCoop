using Common.Messaging;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.BugReportTab;
using GameInterface.Services.UI.CoopOptions.Providers.BugReportTab.Sections;
using GameInterface.Services.UI.Messages;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class BugReportOptionsTests
{
    [Fact]
    public void BugReportOptions_DefaultToShowingTheButton()
    {
        using var messageBroker = new MessageBroker();
        var provider = new BugReportOptionsTabProvider();

        var tab = provider.CreateTab(new CoopOptionsData(), messageBroker, _ => { });

        Assert.Equal(BugReportOptionsTabProvider.TabName, tab.Name);
        Assert.Equal(BugReportOptionsTabProvider.TabId, tab.Id);

        var section = Assert.IsType<BugReportSection>(Assert.Single(tab.Sections));
        Assert.Equal(BugReportSection.SectionId, section.Id);
        Assert.True(section.ShowBugReportButton);
    }

    [Fact]
    public void CoopOptionsMovie_BindsBugReportTabAndButtonVisibility()
    {
        var document = XDocument.Load(FindMoviePath());

        var tab = Assert.Single(document.Descendants("ListPanel"),
            element => element.Attribute("DataSource")?.Value == "{BugReportTab}");
        Assert.Equal("@IsSelected", tab.Attribute("IsVisible")?.Value);
        Assert.Single(tab.Descendants("ButtonWidget"),
            element => element.Attribute("IsSelected")?.Value == "@ShowBugReportButton");
    }

    [Fact]
    public void BugReportButtonHidden_PersistsAndPublishesAfterApply()
    {
        var filePath = CreateTempFilePath();

        try
        {
            var store = new CoopOptionsStore(filePath);
            using var messageBroker = new MessageBroker();
            BugReportVisibilitySelected? selected = null;
            Action<MessagePayload<BugReportVisibilitySelected>> handler = payload => selected = payload.What;
            messageBroker.Subscribe(handler);
            var existingOptions = store.LoadOrDefault();
            existingOptions.SetSection(
                BugReportConsentCoordinator.TabId,
                BugReportConsentCoordinator.SectionId,
                new BugReportConsentOptions
                {
                    ShareBugReportLogs = true,
                    DisclosureVersion = BugReportConsentCoordinator.CurrentDisclosureVersion,
                });
            store.Save(existingOptions);
            var provider = new BugReportOptionsTabProvider();
            var tab = provider.CreateTab(store.LoadOrDefault(), messageBroker, _ => { });
            var section = Assert.IsType<BugReportSection>(Assert.Single(tab.Sections));

            section.ShowBugReportButton = false;
            var options = store.LoadOrDefault();
            tab.Apply(options);
            store.Save(options);
            tab.AfterApply();

            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            var savedSection = document.RootElement
                .GetProperty(BugReportOptionsTabProvider.TabId)
                .GetProperty(BugReportSection.SectionId);
            Assert.False(savedSection.GetProperty("showBugReportButton").GetBoolean());
            Assert.False(savedSection.TryGetProperty("ShowBugReportButton", out _));

            var savedOptions = store.LoadOrDefault();
            Assert.False(BugReportOptionsTabProvider.GetShowBugReportButtonOrDefault(savedOptions));
            Assert.True(savedOptions.TryGetSection(
                BugReportConsentCoordinator.TabId,
                BugReportConsentCoordinator.SectionId,
                out BugReportConsentOptions consent));
            Assert.True(consent.ShareBugReportLogs);
            Assert.Equal(BugReportConsentCoordinator.CurrentDisclosureVersion, consent.DisclosureVersion);
            Assert.True(selected.HasValue);
            Assert.False(selected.Value.ShowBugReportButton);
            GC.KeepAlive(handler);
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
            $"bannerlord-coop-bug-report-options-{Guid.NewGuid():N}.json");
    }

    private static string FindMoviePath([CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", "CoopOptionsUIMovie.xml"));
    }
}
