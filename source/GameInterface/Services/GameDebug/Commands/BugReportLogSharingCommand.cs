using Common;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>Reads or changes the client's diagnostic bug-report log-sharing preference.</summary>
public class BugReportLogSharingCommand
{
    // coop.bug_report_log_sharing status|enable|disable
    [CommandLineArgumentFunction("bug_report_log_sharing", "coop")]
    public static string Configure(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";

        var store = new CoopOptionsStore();
        var preference = new BugReportLogSharingPreference(store);
        if (args == null || args.Count == 0 ||
            args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            return preference.IsEnabled()
                ? "Diagnostic bug-report log sharing is enabled."
                : "Diagnostic bug-report log sharing is disabled.";
        }

        if (args[0].Equals("enable", StringComparison.OrdinalIgnoreCase))
        {
            if (InformationManager.IsAnyInquiryActive())
                return "Close the current prompt before enabling diagnostic bug-report log sharing.";

            var consent = new BugReportConsentCoordinator(
                store,
                exception => InformationManager.DisplayMessage(new InformationMessage(
                    "[Bug Report] Could not save the log-sharing preference: " + exception.Message)));
            consent.ShowPrompt(inquiry => InformationManager.ShowInquiry(inquiry));
            return "Review the diagnostic log-sharing privacy prompt and choose Allow to enable it.";
        }

        if (!args[0].Equals("disable", StringComparison.OrdinalIgnoreCase))
            return "Usage: coop.bug_report_log_sharing status|enable|disable";

        try
        {
            preference.SetEnabled(false);
            return "Diagnostic bug-report log sharing disabled.";
        }
        catch (Exception exception)
        {
            return "Could not save the diagnostic bug-report log-sharing preference: " + exception.Message;
        }
    }
}
