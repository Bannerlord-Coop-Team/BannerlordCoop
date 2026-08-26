using GameInterface.Services.UI.CoopOptions;
using System;
using System.Text.Json.Serialization;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.BugReporting;

/// <summary>Displays and persists the client's diagnostic bug-report log-sharing choice.</summary>
public class BugReportConsentCoordinator
{
    public const string TabId = "BugReporting";
    public const string SectionId = "BugReportLogSharingConsent";
    public const int CurrentDisclosureVersion = 2;
    public const string PromptTitle = "Share Co-op Diagnostic Logs?";
    public const string PromptText =
        "When the dedicated server creates a diagnostic bug report, allow this client's current " +
        "BannerlordCoop log to be sent to the dedicated server, packaged with logs from other consenting " +
        "clients, and submitted to BannerlordCoop's bug-report service? Reports create a public GitHub " +
        "issue containing the reporting player's network ID. Included server and client logs are uploaded " +
        "to publicly accessible links, and remote deletion or expiry is not guaranteed. Reports may be triggered " +
        "automatically by recovery actions such as the coop unstuck command. Logs may contain " +
        "player names, Steam IDs, IP addresses, file paths, and gameplay or network details. Runtime " +
        "command arguments and common credential values are redacted. Saves, configuration files, " +
        "and memory dumps are not included. Choose No thanks to keep this client's log local. " +
        "Change this later with coop.bug_report_log_sharing enable or disable.";

    private readonly ICoopOptionsStore optionsStore;
    private readonly Action<Exception> reportError;
    private bool loaded;
    private bool promptShown;
    private bool? decision;

    public BugReportConsentCoordinator(
        ICoopOptionsStore optionsStore,
        Action<Exception> reportError)
    {
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        this.optionsStore = optionsStore;
        this.reportError = reportError;
    }

    public void TryShowPrompt(bool canShow, Action<InquiryData> showInquiry)
    {
        if (!canShow || promptShown) return;
        if (showInquiry == null) throw new ArgumentNullException(nameof(showInquiry));

        Load();
        if (decision.HasValue) return;

        promptShown = true;
        showInquiry(new InquiryData(
            PromptTitle,
            PromptText,
            true,
            true,
            "Allow",
            "No thanks",
            () => RecordDecision(true),
            () => RecordDecision(false)));
    }

    private void Load()
    {
        if (loaded) return;

        loaded = true;
        var options = optionsStore.LoadOrDefault();
        if (options.TryGetSection(
            TabId,
            SectionId,
            out BugReportConsentOptions saved))
        {
            decision = saved.DisclosureVersion == CurrentDisclosureVersion
                ? saved.ShareBugReportLogs
                : null;
        }
    }

    private void RecordDecision(bool enabled)
    {
        decision = enabled;
        try
        {
            var options = optionsStore.LoadOrDefault();
            options.SetSection(
                TabId,
                SectionId,
                new BugReportConsentOptions
                {
                    ShareBugReportLogs = enabled,
                    DisclosureVersion = CurrentDisclosureVersion,
                });
            optionsStore.Save(options);
        }
        catch (Exception exception)
        {
            reportError?.Invoke(exception);
        }
    }
}

/// <summary>Stores the client's diagnostic bug-report log-sharing consent state.</summary>
public class BugReportConsentOptions
{
    [JsonPropertyName("shareBugReportLogs")]
    public bool? ShareBugReportLogs { get; set; }

    [JsonPropertyName("disclosureVersion")]
    public int? DisclosureVersion { get; set; }
}
