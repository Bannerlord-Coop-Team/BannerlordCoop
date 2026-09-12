using GameInterface.Services.UI.CoopOptions;
using System;
using System.Text.Json.Serialization;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CrashReporting;

public class CrashReportingConsentCoordinator
{
    public const string TabId = "CrashReporting";
    public const string SectionId = "Consent";
    public const string PromptTitle = "Automatic Crash Reports";
    public const string PromptText =
        "Enable Bannerlord's automatic crash-report mode? " +
        "A full report may include process memory, logs, configuration files, and your newest save. " +
        "BannerlordCoop separately keeps local crash diagnostics and never uploads them. " +
        "Its shareable ZIP includes a memory dump when Bannerlord creates one, but excludes saves and configuration files.";

    private readonly ICoopOptionsStore optionsStore;
    private readonly Action requestAutoreport;
    private readonly Action<Exception> reportError;
    private bool loaded;
    private bool promptShown;
    private bool? decision;

    public CrashReportingConsentCoordinator(
        ICoopOptionsStore optionsStore,
        Action requestAutoreport,
        Action<Exception> reportError)
    {
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        if (requestAutoreport == null) throw new ArgumentNullException(nameof(requestAutoreport));

        this.optionsStore = optionsStore;
        this.requestAutoreport = requestAutoreport;
        this.reportError = reportError;
    }

    public void ApplyStoredDecision()
    {
        Load();
        if (decision == true)
            requestAutoreport();
    }

    public void TryShowPrompt(bool canShow, Action<InquiryData> showInquiry)
    {
        if (!canShow || promptShown)
            return;

        Load();
        if (decision.HasValue)
            return;

        promptShown = true;
        showInquiry(new InquiryData(
            PromptTitle,
            PromptText,
            true,
            true,
            "Enable",
            "No thanks",
            () => RecordDecision(true),
            () => RecordDecision(false)));
    }

    private void Load()
    {
        if (loaded)
            return;

        loaded = true;
        CoopOptionsData options = optionsStore.LoadOrDefault();
        if (options.TryGetSection(
            TabId,
            SectionId,
            out CrashReportingConsentOptions saved))
        {
            decision = saved.AutomaticCrashReports;
        }
    }

    private void RecordDecision(bool enabled)
    {
        decision = enabled;
        if (enabled)
            requestAutoreport();

        try
        {
            CoopOptionsData options = optionsStore.LoadOrDefault();
            options.SetSection(
                TabId,
                SectionId,
                new CrashReportingConsentOptions { AutomaticCrashReports = enabled });
            optionsStore.Save(options);
        }
        catch (Exception exception)
        {
            reportError?.Invoke(exception);
        }
    }
}

public class CrashReportingConsentOptions
{
    [JsonPropertyName("automaticCrashReports")]
    public bool? AutomaticCrashReports { get; set; }
}
