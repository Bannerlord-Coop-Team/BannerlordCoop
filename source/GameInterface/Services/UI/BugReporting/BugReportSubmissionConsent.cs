using Serilog;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.BugReporting;

/// <summary>Creates the privacy confirmation required before submitting a bug report.</summary>
public interface IBugReportSubmissionConsent
{
    bool IsRequired();
    InquiryData CreateInquiry(Action allow, Action decline);
}

/// <inheritdoc />
public class BugReportSubmissionConsent : IBugReportSubmissionConsent
{
    public const string PromptTitle = "Share Diagnostics and Submit Bug Report?";
    public const string PromptText =
        "Submitting this report sends your network ID, summary, and description to the dedicated " +
        "server and BannerlordCoop's bug-report service. The reporting network ID is published in a " +
        "public GitHub issue. The server creates and uploads its current campaign save and paired co-op " +
        "session data with the report. The save data and included server and client logs are uploaded to " +
        "publicly accessible links, and remote " +
        "deletion or expiry is not guaranteed. Allowing also enables sharing this client's current " +
        "BannerlordCoop log with the report. The server save and logs may contain player names, Steam IDs, " +
        "IP addresses, file paths, and gameplay or network details. Runtime command arguments and common " +
        "credential values are redacted from logs. Client saves, configuration files, and memory dumps are " +
        "not included. Choose Allow to enable diagnostic log sharing and submit this report. " +
        "Choose No thanks to cancel this bug report. Change log sharing later with " +
        "coop.bug_report_log_sharing enable or disable.";

    private readonly IBugReportLogSharingPreference preference;
    private readonly ILogger logger;

    public BugReportSubmissionConsent(
        IBugReportLogSharingPreference preference,
        ILogger logger)
    {
        if (preference == null) throw new ArgumentNullException(nameof(preference));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        this.preference = preference;
        this.logger = logger;
    }

    public bool IsRequired() => !preference.IsEnabled();

    public InquiryData CreateInquiry(Action allow, Action decline)
    {
        if (allow == null) throw new ArgumentNullException(nameof(allow));
        if (decline == null) throw new ArgumentNullException(nameof(decline));

        return new InquiryData(
            PromptTitle,
            PromptText,
            true,
            true,
            "Allow",
            "No thanks",
            () =>
            {
                SaveDecision(true);
                allow();
            },
            () =>
            {
                SaveDecision(false);
                decline();
            });
    }

    private void SaveDecision(bool enabled)
    {
        try
        {
            preference.SetEnabled(enabled);
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "Bug-report log-sharing preference could not be saved");
        }
    }
}
