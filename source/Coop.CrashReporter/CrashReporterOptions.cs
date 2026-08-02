using System;
using System.Globalization;
using System.IO;

namespace Coop.CrashReporter
{
    internal sealed class CrashReporterOptions
    {
        private const string BannerlordDirectoryName = "Mount and Blade II Bannerlord";

        internal CrashReporterOptions(
            int processId,
            long processStartUtcTicks,
            string coopLogPath,
            string role,
            string build,
            string programDataRoot,
            string outputRoot)
        {
            ProcessId = processId;
            ProcessStartUtcTicks = processStartUtcTicks;
            CoopLogPath = Path.GetFullPath(coopLogPath);
            Role = role;
            Build = build;
            BannerlordDataRoot = Path.Combine(
                Path.GetFullPath(programDataRoot),
                BannerlordDirectoryName);
            OutputRoot = Path.GetFullPath(outputRoot);
        }

        public int ProcessId { get; }
        public long ProcessStartUtcTicks { get; }
        public string CoopLogPath { get; }
        public string Role { get; }
        public string Build { get; }
        public string BannerlordDataRoot { get; }
        public string OutputRoot { get; }

        public static bool TryParse(string[] args, out CrashReporterOptions options)
        {
            options = null;
            int processId;
            long startTicks;
            string logPath = Environment.GetEnvironmentVariable("COOP_CRASH_LOG");
            if (args == null ||
                args.Length != 2 ||
                !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out processId) ||
                !long.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out startTicks) ||
                processId <= 0 ||
                startTicks <= 0 ||
                string.IsNullOrWhiteSpace(logPath))
            {
                return false;
            }

            string programDataRoot =
                Environment.GetEnvironmentVariable("COOP_CRASH_PROGRAM_DATA") ??
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string outputRoot =
                Environment.GetEnvironmentVariable("COOP_CRASH_OUTPUT") ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    BannerlordDirectoryName,
                    "Coop Crash Reports");

            try
            {
                options = new CrashReporterOptions(
                    processId,
                    startTicks,
                    logPath,
                    Environment.GetEnvironmentVariable("COOP_CRASH_ROLE") ?? "unknown",
                    Environment.GetEnvironmentVariable("COOP_CRASH_BUILD") ?? "unknown",
                    programDataRoot,
                    outputRoot);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return false;
            }
        }
    }
}
