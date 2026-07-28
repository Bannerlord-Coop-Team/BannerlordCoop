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
        private readonly CrashReporterOptions options;

        public CrashReportCollector(CrashReporterOptions options)
        {
            this.options = options;
        }

        public int Run(Process process)
        {
            process.WaitForExit();
            int exitCode = process.ExitCode;
            string dumpPath = WaitForMatchingDump();
            if (exitCode == 0 && dumpPath == null)
                return 0;

            string reportDirectory = CreateReportDirectory();
            string logsDirectory = Path.Combine(reportDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            bool dumpCopied = dumpPath != null &&
                              TryCopyDump(dumpPath, Path.Combine(reportDirectory, "dump.dmp"));
            List<string> copiedLogs = CopyLogs(logsDirectory);

            string reportPath = Path.Combine(reportDirectory, "report.txt");
            string readmePath = Path.Combine(reportDirectory, "README.txt");
            WriteReport(reportPath, exitCode, dumpCopied, copiedLogs);
            WriteReadme(readmePath, dumpCopied);
            CreateShareableZip(
                Path.Combine(reportDirectory, "shareable.zip"),
                readmePath,
                reportPath,
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

        private string WaitForMatchingDump()
        {
            DateTime processStartUtc =
                new DateTime(options.ProcessStartUtcTicks, DateTimeKind.Utc)
                    .AddSeconds(-1);
            for (var attempt = 0; attempt < 20; attempt++)
            {
                foreach (string dumpPath in FindDumps())
                {
                    int dumpProcessId;
                    if (File.GetLastWriteTimeUtc(dumpPath) >= processStartUtc &&
                        MinidumpProcessIdReader.TryReadProcessId(dumpPath, out dumpProcessId) &&
                        dumpProcessId == options.ProcessId)
                    {
                        return dumpPath;
                    }
                }

                Thread.Sleep(250);
            }

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

        private bool TryCopyDump(string sourcePath, string destinationPath)
        {
            for (var attempt = 0; attempt < 20; attempt++)
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
                    if (sourceLength > 0 &&
                        new FileInfo(destinationPath).Length == sourceLength &&
                        MinidumpProcessIdReader.TryReadProcessId(
                            destinationPath,
                            out copiedProcessId) &&
                        copiedProcessId == options.ProcessId)
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                TryDelete(destinationPath);
                Thread.Sleep(250);
            }

            TryDelete(destinationPath);
            return false;
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
                "Logs captured: " + copiedLogs.Count().ToString(CultureInfo.InvariantCulture),
            };
            File.WriteAllLines(reportPath, lines, new UTF8Encoding(false));
        }

        private static void WriteReadme(string readmePath, bool dumpCopied)
        {
            var lines = new List<string>
            {
                "BannerlordCoop created this folder locally after Bannerlord exited unexpectedly.",
                "BannerlordCoop did not upload this folder or any of its files.",
                "If you enabled Bannerlord automatic crash reports, Bannerlord may separately submit its own report to TaleWorlds.",
                "",
                "Send shareable.zip to the BannerlordCoop team after reviewing it.",
                "Logs can contain player names, file paths, and server details.",
                "The ZIP does not contain a memory dump, save, or configuration file.",
            };
            if (dumpCopied)
            {
                lines.Add("");
                lines.Add("dump.dmp is kept only in this local folder for advanced debugging.");
            }

            File.WriteAllLines(readmePath, lines, new UTF8Encoding(false));
        }

        private static void CreateShareableZip(
            string zipPath,
            string readmePath,
            string reportPath,
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
