using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace Coop.CrashReporter
{
    internal sealed class CrashReportCollector
    {
        private const string RuntimeArgumentsMarker = "#TW#Runtime#TW#Arguments#TW#";
        private static readonly TimeSpan CrashDumpDiscoveryTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DumpCompletionTimeout = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(250);
        private readonly CrashReporterOptions options;
        private readonly TimeSpan dumpCompletionTimeout;
        private readonly TimeSpan retryInterval;
        private readonly Func<string, string, bool> tryCopyDumpAttempt;

        public CrashReportCollector(CrashReporterOptions options)
            : this(options, DumpCompletionTimeout, RetryInterval, null)
        {
        }

        internal CrashReportCollector(
            CrashReporterOptions options,
            TimeSpan dumpCompletionTimeout,
            TimeSpan retryInterval)
            : this(options, dumpCompletionTimeout, retryInterval, null)
        {
        }

        internal CrashReportCollector(
            CrashReporterOptions options,
            TimeSpan dumpCompletionTimeout,
            TimeSpan retryInterval,
            Func<string, string, bool> tryCopyDumpAttempt)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
#pragma warning disable CA1512 // ThrowIfLessThanOrEqual is unavailable on .NET Framework 4.7.2.
            if (dumpCompletionTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(dumpCompletionTimeout));
            if (retryInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(retryInterval));
#pragma warning restore CA1512

            this.options = options;
            this.dumpCompletionTimeout = dumpCompletionTimeout;
            this.retryInterval = retryInterval;
            this.tryCopyDumpAttempt = tryCopyDumpAttempt ?? TryCopyDumpOnce;
        }

        public int Run(Process process)
        {
            process.WaitForExit();
            int exitCode = process.ExitCode;

            // Walked before anything waits on a dump: the zero exit-code path spends up to
            // 30 seconds looking for one, and a launcher update inside that window would rewrite
            // the very files being listed. A clean exit pays for a walk whose result is then
            // thrown away, but the game's own binaries are still in the file cache it just used
            // them from, so that is a fraction of a second in a process about to exit anyway.
            GameBinariesManifest binaries = GameBinariesManifest.Capture(
                options.GameBinariesDirectory);

            string dumpPath = exitCode == 0
                ? WaitForMatchingDump(CrashDumpDiscoveryTimeout)
                : null;
            if (exitCode == 0 && dumpPath == null)
                return 0;

            string reportDirectory = CreateReportDirectory();
            string logsDirectory = Path.Combine(reportDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);
            List<string> copiedLogs = CopyLogs(logsDirectory);

            string manifestPath = Path.Combine(reportDirectory, GameBinariesManifest.FileName);
            bool manifestWritten;
            string binariesSummary = binaries.Write(manifestPath, out manifestWritten);

            if (dumpPath == null)
                dumpPath = WaitForMatchingDump(CrashDumpDiscoveryTimeout);

            string copiedDumpPath = Path.Combine(reportDirectory, "dump.dmp");
            bool dumpCopied = dumpPath != null &&
                              TryCopyDump(dumpPath, copiedDumpPath);
            string dumpCaptureDetails = GetDumpCaptureDetails(dumpPath, dumpCopied);
            RefreshLogs(logsDirectory, copiedLogs);

            string reportPath = Path.Combine(reportDirectory, "report.txt");
            string readmePath = Path.Combine(reportDirectory, "README.txt");
            WriteReport(
                reportPath,
                exitCode,
                dumpCopied,
                dumpCaptureDetails,
                binariesSummary,
                copiedLogs);
            WriteReadme(readmePath, dumpCopied, manifestWritten);
            CreateShareableZip(
                Path.Combine(reportDirectory, "shareable.zip"),
                readmePath,
                reportPath,
                dumpCopied ? copiedDumpPath : null,
                manifestWritten ? manifestPath : null,
                copiedLogs);
            return 0;
        }

        private HashSet<string> FindDumps()
        {
            string crashesRoot = Path.Combine(options.BannerlordDataRoot, "crashes");
            if (!Directory.Exists(crashesRoot))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                return Directory
                    .EnumerateFiles(crashesRoot, "*.dmp", SearchOption.AllDirectories)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (UnauthorizedAccessException)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        internal string WaitForMatchingDump(TimeSpan initialTimeout)
        {
            DateTime processStartUtc =
                new DateTime(options.ProcessStartUtcTicks, DateTimeKind.Utc)
                    .AddSeconds(-1);
            var stopwatch = Stopwatch.StartNew();
            do
            {
                foreach (string dumpPath in FindDumps())
                {
                    int dumpProcessId;
                    if (File.GetLastWriteTimeUtc(dumpPath) < processStartUtc)
                        continue;

                    if (MinidumpProcessIdReader.TryReadProcessId(
                            dumpPath,
                            out dumpProcessId) &&
                        dumpProcessId == options.ProcessId)
                    {
                        return dumpPath;
                    }
                }

                WaitForRetry(stopwatch, initialTimeout);
            }
            while (stopwatch.Elapsed < initialTimeout);

            return null;
        }

        private string CreateReportDirectory()
        {
            string role = options.Role.Equals("server", StringComparison.OrdinalIgnoreCase)
                ? "server"
                : "client";
            string name = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd_HH-mm-ss}_{1}_{2}_{3}",
                DateTime.Now,
                role,
                options.ProcessId,
                Guid.NewGuid().ToString("N").Substring(0, 8));
            string path = Path.Combine(options.OutputRoot, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private List<string> CopyLogs(string destinationRoot)
        {
            var copied = new List<string>();
            CopyLog(options.CoopLogPath, destinationRoot, copied);

            string sourceRoot = Path.Combine(options.BannerlordDataRoot, "logs");
            string processId = options.ProcessId.ToString(CultureInfo.InvariantCulture);
            CopyLog(
                Path.Combine(sourceRoot, "watchdog_log_" + processId + ".txt"),
                destinationRoot,
                copied);
            CopyLog(
                Path.Combine(sourceRoot, "rgl_log_" + processId + ".txt"),
                destinationRoot,
                copied);
            CopyLog(
                Path.Combine(sourceRoot, "rgl_log_errors_" + processId + ".txt"),
                destinationRoot,
                copied);
            return copied;
        }

        private void RefreshLogs(string destinationRoot, ICollection<string> copiedLogs)
        {
            foreach (string refreshedLog in CopyLogs(destinationRoot))
            {
                if (!copiedLogs.Contains(refreshedLog, StringComparer.OrdinalIgnoreCase))
                    copiedLogs.Add(refreshedLog);
            }
        }

        private static void CopyLog(
            string sourcePath,
            string destinationRoot,
            ICollection<string> copied)
        {
            string destinationPath = Path.Combine(
                destinationRoot,
                Path.GetFileName(sourcePath));
            if (TryCopyFile(sourcePath, destinationPath))
                copied.Add(destinationPath);
        }

        internal bool TryCopyDump(string sourcePath, string destinationPath)
        {
            var stopwatch = Stopwatch.StartNew();
            do
            {
                if (tryCopyDumpAttempt(sourcePath, destinationPath))
                    return true;

                TryDelete(destinationPath);
                WaitForRetry(stopwatch, dumpCompletionTimeout);
            }
            while (stopwatch.Elapsed < dumpCompletionTimeout);

            TryDelete(destinationPath);
            return false;
        }

        private bool TryCopyDumpOnce(string sourcePath, string destinationPath)
        {
            try
            {
                long sourceLength;
                using (var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                using (var destination = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    source.CopyTo(destination);
                    sourceLength = source.Length;
                }

                int copiedProcessId;
                return sourceLength > 0 &&
                       new FileInfo(destinationPath).Length == sourceLength &&
                       MinidumpProcessIdReader.TryReadProcessId(
                           destinationPath,
                           out copiedProcessId) &&
                       copiedProcessId == options.ProcessId;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void WaitForRetry(Stopwatch stopwatch, TimeSpan timeout)
        {
            TimeSpan remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return;

            Thread.Sleep(remaining < retryInterval ? remaining : retryInterval);
        }

        private static string GetDumpCaptureDetails(string dumpPath, bool dumpCopied)
        {
            if (dumpCopied)
                return "matching dump copied and validated";

            return dumpPath == null
                ? "no matching readable dump appeared before the discovery timeout"
                : "matching dump did not become copyable before the completion timeout";
        }

        private static bool TryCopyFile(string sourcePath, string destinationPath)
        {
            try
            {
                using (var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var destination = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    source.CopyTo(destination);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void WriteReport(
            string reportPath,
            int exitCode,
            bool dumpCopied,
            string dumpCaptureDetails,
            string binariesSummary,
            IEnumerable<string> copiedLogs)
        {
            var lines = new[]
            {
                "BannerlordCoop crash report",
                "Captured: " + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
                "Process id: " + options.ProcessId.ToString(CultureInfo.InvariantCulture),
                "Role: " + options.Role,
                "Build: " + options.Build,
                "Exit code: " + exitCode.ToString(CultureInfo.InvariantCulture),
                "Dump captured: " + (dumpCopied ? "yes" : "no"),
                "Dump capture details: " + dumpCaptureDetails,
                "Game binaries directory: " + (options.GameBinariesDirectory ?? "unknown"),
                "Game binaries: " + binariesSummary,
                "Logs captured: " + copiedLogs.Count().ToString(CultureInfo.InvariantCulture),
            };
            File.WriteAllLines(reportPath, lines, new UTF8Encoding(false));
        }

        private static void WriteReadme(
            string readmePath,
            bool dumpCopied,
            bool manifestWritten)
        {
            var lines = new List<string>
            {
                "BannerlordCoop created this folder locally after Bannerlord exited unexpectedly.",
                "BannerlordCoop did not upload this folder or any of its files.",
                "If you enabled Bannerlord automatic crash reports, Bannerlord may separately submit its own report to TaleWorlds.",
                "",
                "Send shareable.zip to the BannerlordCoop team after reviewing it.",
                "Memory dumps and logs can contain player names, file paths, server details, and other process data.",
                "The ZIP does not contain a save or configuration file.",
            };
            if (dumpCopied)
            {
                lines.Add("");
                lines.Add("The ZIP contains dump.dmp for advanced debugging.");
            }

            if (manifestWritten)
            {
                lines.Add("");
                lines.Add(
                    GameBinariesManifest.FileName +
                    " lists the file names, sizes and versions found in the game's binaries folder.");
            }

            File.WriteAllLines(readmePath, lines, new UTF8Encoding(false));
        }

        private static void CreateShareableZip(
            string zipPath,
            string readmePath,
            string reportPath,
            string dumpPath,
            string manifestPath,
            IEnumerable<string> copiedLogs)
        {
            using (var stream = new FileStream(
                zipPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddFile(archive, readmePath, "README.txt", false);
                AddFile(archive, reportPath, "report.txt", false);
                if (dumpPath != null)
                    AddFile(archive, dumpPath, "dump.dmp", false);
                if (manifestPath != null)
                    AddFile(archive, manifestPath, GameBinariesManifest.FileName, false);

                foreach (string logPath in copiedLogs)
                {
                    AddFile(
                        archive,
                        logPath,
                        "logs/" + Path.GetFileName(logPath),
                        true);
                }
            }
        }

        private static void AddFile(
            ZipArchive archive,
            string sourcePath,
            string entryName,
            bool redactRuntimeArguments)
        {
            ZipArchiveEntry entry = archive.CreateEntry(
                entryName,
                CompressionLevel.Optimal);
            using (Stream destination = entry.Open())
            {
                if (!redactRuntimeArguments)
                {
                    using (var source = File.OpenRead(sourcePath))
                        source.CopyTo(destination);
                    return;
                }

                using (var source = File.OpenRead(sourcePath))
                using (var reader = new StreamReader(source, Encoding.UTF8, true))
                using (var writer = new StreamWriter(destination, new UTF8Encoding(false)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int markerIndex = line.IndexOf(
                            RuntimeArgumentsMarker,
                            StringComparison.OrdinalIgnoreCase);
                        if (markerIndex >= 0)
                        {
                            line = line.Substring(
                                0,
                                markerIndex + RuntimeArgumentsMarker.Length) +
                                "[redacted from shareable ZIP]";
                        }
                        else
                        {
                            const string commandArgumentsMarker = "Command Args:";
                            markerIndex = line.IndexOf(
                                commandArgumentsMarker,
                                StringComparison.OrdinalIgnoreCase);
                            if (markerIndex >= 0)
                            {
                                line = line.Substring(
                                    0,
                                    markerIndex + commandArgumentsMarker.Length) +
                                    " [redacted from shareable ZIP]";
                            }
                        }

                        writer.WriteLine(line);
                    }
                }
            }
        }
    }
}
