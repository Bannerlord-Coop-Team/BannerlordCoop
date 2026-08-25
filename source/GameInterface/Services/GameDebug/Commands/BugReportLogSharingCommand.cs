using Common;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using System;
using System.Collections.Generic;
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

        bool enabled;
        if (args[0].Equals("enable", StringComparison.OrdinalIgnoreCase))
        {
            enabled = true;
        }
        else if (args[0].Equals("disable", StringComparison.OrdinalIgnoreCase))
        {
            enabled = false;
        }
        else
        {
            return "Usage: coop.bug_report_log_sharing status|enable|disable";
        }

        try
        {
            preference.SetEnabled(enabled);
            return enabled
                ? "Diagnostic bug-report log sharing enabled."
                : "Diagnostic bug-report log sharing disabled.";
        }
        catch (Exception exception)
        {
            return "Could not save the diagnostic bug-report log-sharing preference: " + exception.Message;
        }
    }
}
