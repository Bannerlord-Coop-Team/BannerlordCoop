using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using Moq;
using Serilog;
using System;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Tests the persisted diagnostic bug-report log-sharing consent flow.</summary>
public class BugReportConsentTests
{
    [Fact]
    public void PromptText_ExplainsScopeAndSensitiveLogData()
    {
        Assert.Contains("sent to the dedicated server", BugReportConsentCoordinator.PromptText);
        Assert.Contains("IP addresses", BugReportConsentCoordinator.PromptText);
        Assert.Contains("Saves, configuration files, and memory dumps are not included",
            BugReportConsentCoordinator.PromptText);
        Assert.Contains("cancel this bug report", BugReportSubmissionConsent.PromptText);
        Assert.Contains("public GitHub issue", BugReportConsentCoordinator.PromptText);
        Assert.Contains("publicly accessible links", BugReportConsentCoordinator.PromptText);
        Assert.Contains("remote deletion or expiry is not guaranteed", BugReportConsentCoordinator.PromptText);
        Assert.Contains("public GitHub issue", BugReportSubmissionConsent.PromptText);
        Assert.Contains("publicly accessible links", BugReportSubmissionConsent.PromptText);
        Assert.Contains("remote deletion or expiry is not guaranteed", BugReportSubmissionConsent.PromptText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PromptDecision_IsSavedAndControlsPreference(bool enabled)
    {
        var store = new TestOptionsStore();
        var coordinator = new BugReportConsentCoordinator(store, _ => { });
        InquiryData inquiry = null;

        coordinator.TryShowPrompt(true, value => inquiry = value);
        Assert.NotNull(inquiry);
        if (enabled)
            inquiry.AffirmativeAction();
        else
            inquiry.NegativeAction();

        var preference = new BugReportLogSharingPreference(store);
        Assert.Equal(enabled, preference.IsEnabled());
    }

    [Fact]
    public void LegacyConsentWithoutPublicDisclosure_IsDisabledAndPromptedAgain()
    {
        var store = new TestOptionsStore();
        var options = store.LoadOrDefault();
        options.SetSection(
            BugReportConsentCoordinator.TabId,
            BugReportConsentCoordinator.SectionId,
            new BugReportConsentOptions { ShareBugReportLogs = true });
        store.Save(options);
        var coordinator = new BugReportConsentCoordinator(store, _ => { });
        InquiryData inquiry = null;

        coordinator.TryShowPrompt(true, value => inquiry = value);

        Assert.NotNull(inquiry);
        Assert.False(new BugReportLogSharingPreference(store).IsEnabled());
    }

    [Fact]
    public void MissingDecision_DefaultsToDisabled()
    {
        var preference = new BugReportLogSharingPreference(new TestOptionsStore());

        Assert.False(preference.IsEnabled());
    }

    [Fact]
    public void SubmissionPrompt_AllowEnablesSharingAndContinuesReport()
    {
        var preference = new BugReportLogSharingPreference(new TestOptionsStore());
        var consent = new BugReportSubmissionConsent(preference, Mock.Of<ILogger>());
        var allowed = false;
        var declined = false;

        var inquiry = consent.CreateInquiry(
            () => allowed = true,
            () => declined = true);
        inquiry.AffirmativeAction();

        Assert.True(allowed);
        Assert.False(declined);
        Assert.True(preference.IsEnabled());
        Assert.False(consent.IsRequired());
    }

    [Fact]
    public void SubmissionPrompt_NoThanksKeepsSharingDisabledAndDiscardsReport()
    {
        var preference = new BugReportLogSharingPreference(new TestOptionsStore());
        var consent = new BugReportSubmissionConsent(preference, Mock.Of<ILogger>());
        var allowed = false;
        var declined = false;

        var inquiry = consent.CreateInquiry(
            () => allowed = true,
            () => declined = true);
        inquiry.NegativeAction();

        Assert.False(allowed);
        Assert.True(declined);
        Assert.False(preference.IsEnabled());
        Assert.True(consent.IsRequired());
    }

    [Fact]
    public void SubmissionPrompt_NoThanksDiscardsReportWhenPreferenceCannotBeSaved()
    {
        var preference = new Mock<IBugReportLogSharingPreference>();
        preference.Setup(value => value.SetEnabled(false)).Throws<InvalidOperationException>();
        var consent = new BugReportSubmissionConsent(preference.Object, Mock.Of<ILogger>());
        var declined = false;

        var inquiry = consent.CreateInquiry(() => { }, () => declined = true);
        inquiry.NegativeAction();

        Assert.True(declined);
    }

    private sealed class TestOptionsStore : ICoopOptionsStore
    {
        public string FilePath => string.Empty;
        public CoopOptionsData Options { get; private set; } = new CoopOptionsData();

        public bool TryLoad(out CoopOptionsData options)
        {
            options = Options;
            return true;
        }

        public CoopOptionsData LoadOrDefault() => Options;

        public void Save(CoopOptionsData options)
        {
            Options = options;
        }
    }
}
