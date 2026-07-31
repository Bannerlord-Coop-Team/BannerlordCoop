using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CrashReporting;
using System;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class CrashReportingConsentTests
{
    [Fact]
    public void PromptText_ExplainsDumpInShareableZip()
    {
        Assert.Contains(
            "shareable ZIP includes a memory dump",
            CrashReportingConsentCoordinator.PromptText);
        Assert.Contains(
            "excludes saves and configuration files",
            CrashReportingConsentCoordinator.PromptText);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void StoredDecision_ControlsAutoreport(bool savedDecision, int expectedRequests)
    {
        var store = new TestOptionsStore(CreateOptions(savedDecision));
        var requests = 0;
        var coordinator = new CrashReportingConsentCoordinator(
            store,
            () => requests++,
            _ => { });

        coordinator.ApplyStoredDecision();

        Assert.Equal(expectedRequests, requests);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void PromptDecision_IsSavedAndPromptIsNotRepeated(
        bool enabled,
        int expectedRequests)
    {
        var store = new TestOptionsStore();
        var requests = 0;
        var shown = 0;
        InquiryData inquiry = null;
        var coordinator = new CrashReportingConsentCoordinator(
            store,
            () => requests++,
            _ => { });

        coordinator.TryShowPrompt(true, value =>
        {
            shown++;
            inquiry = value;
        });
        coordinator.TryShowPrompt(true, _ => shown++);
        if (enabled)
            inquiry.AffirmativeAction();
        else
            inquiry.NegativeAction();

        Assert.Equal(1, shown);
        Assert.Equal(expectedRequests, requests);
        Assert.Equal(enabled, GetDecision(store));
    }

    private static CoopOptionsData CreateOptions(bool decision)
    {
        var options = new CoopOptionsData();
        options.SetSection(
            CrashReportingConsentCoordinator.TabId,
            CrashReportingConsentCoordinator.SectionId,
            new CrashReportingConsentOptions { AutomaticCrashReports = decision });
        return options;
    }

    private static bool GetDecision(TestOptionsStore store)
    {
        Assert.True(store.Options.TryGetSection(
            CrashReportingConsentCoordinator.TabId,
            CrashReportingConsentCoordinator.SectionId,
            out CrashReportingConsentOptions consent));
        return consent.AutomaticCrashReports.Value;
    }

    private sealed class TestOptionsStore : ICoopOptionsStore
    {
        public TestOptionsStore(CoopOptionsData options = null)
        {
            Options = options ?? new CoopOptionsData();
        }

        public string FilePath => string.Empty;
        public CoopOptionsData Options { get; private set; }

        public bool TryLoad(out CoopOptionsData options)
        {
            options = Options;
            return true;
        }

        public CoopOptionsData LoadOrDefault()
        {
            return Options;
        }

        public void Save(CoopOptionsData options)
        {
            Options = options;
        }
    }
}
