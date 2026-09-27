#if DEBUG
using Common;
using System.Collections.Generic;
using System.Threading;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Coop.Core.Common.Commands;

/// <summary>
/// Overrides the co-op build a DEBUG client reports when it joins, to stage a refused join.
/// </summary>
public static class JoinBuildDebugCommands
{
    private const string ClearArgument = "clear";

    private static string reportedBuildOverride;

    /// <summary>
    /// The build a joining client reports: the override when one is set, otherwise the real build.
    /// </summary>
    public static string ReportedBuildVersion => Volatile.Read(ref reportedBuildOverride) ?? ModInformation.BuildVersion;

    // Attributed like reconnect, so the in-game console still reaches it after a refused join tore the session down.
    [CommandLineArgumentFunction("report_build", "coop.debug.connection")]
    public static string ReportBuild(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.connection.report_build <build|clear>";
        }
        if (ModInformation.IsServer)
        {
            return "report_build must be run on a client.";
        }

        string build = args[0] == ClearArgument ? null : args[0];
        Volatile.Write(ref reportedBuildOverride, build);
        return $"Joins now report co-op build '{ReportedBuildVersion}'.";
    }

    internal static void ResetReportedBuild()
    {
        Volatile.Write(ref reportedBuildOverride, null);
    }
}
#endif
