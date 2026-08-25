using System;

namespace Coop.Core.Diagnostics;

/// <summary>
/// Threads the resolved build version through crash-reporting startup so a caller cannot
/// initialize crash reporting or emit the log header before the version is resolved.
/// </summary>
public static class StartupDiagnosticsSequence
{
    public static void Run(
        Func<string> resolveInformationalVersion,
        Action<string> emitLogHeader,
        Action<string> initializeCrashReporting)
    {
        string version = resolveInformationalVersion();
        emitLogHeader(version);
        initializeCrashReporting(version);
    }
}
