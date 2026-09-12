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
        Action<string> initializeCrashReporting,
        Func<string> resolveInformationalVersion = null)
    {
        string version = (resolveInformationalVersion ?? DefaultResolveInformationalVersion)();
        
        try
        {
            emitLogHeader(version);
        }
        catch
        {
            // Best effort
        }
        
        initializeCrashReporting(version);
    }

    private static string DefaultResolveInformationalVersion() => ModInformation.BuildVersion;
}
