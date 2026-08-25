using System;
using Common;

namespace Coop.Core.Diagnostics;

/// <summary>
/// Threads the resolved build version through crash-reporting startup so a caller cannot
/// initialize crash reporting or emit the log header before the version is resolved.
/// </summary>
public static class StartupDiagnosticsSequence
{
    public static void Run(
        Action<string> emitLogHeader,
        Action<string> initializeCrashReporting)
    {
        string version = ModInformation.BuildVersion;
        emitLogHeader(version);
        initializeCrashReporting(version);
    }
}
