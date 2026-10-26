using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.CrashReporter.Tests
{
    public sealed class CrashReportCollectorTests : IDisposable
    {
        private readonly string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "CoopCrashReporterTests_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public async Task WaitForMatchingDump_RetriesPastTwentyIntervals()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            var collector = new CrashReportCollector(
                options,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(10));

            Task<string> waitTask = Task.Run(
                () => collector.WaitForMatchingDump(TimeSpan.FromSeconds(2)));

            await Task.Delay(300);
            string dumpPath = Path.Combine(options.BannerlordDataRoot, "crashes", "dump.dmp");
            WriteMinidump(dumpPath, processId);

            Assert.Equal(dumpPath, await waitTask);
        }

        [Fact]
        public void TryCopyDump_RetriesPastTwentyIntervals()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            var attempts = 0;
            var collector = new CrashReportCollector(
                options,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(10),
                (source, destination) => Interlocked.Increment(ref attempts) > 25);

            Assert.True(collector.TryCopyDump(
                Path.Combine(tempRoot, "source.dmp"),
                Path.Combine(tempRoot, "destination.dmp")));
            Assert.True(attempts > 20);
        }

        [Fact]
        public void TryCopyDump_CopiesAndValidatesMatchingDump()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            var collector = new CrashReportCollector(
                options,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(10));
            string sourcePath = Path.Combine(tempRoot, "source.dmp");
            string destinationPath = Path.Combine(tempRoot, "copied.dmp");
            WriteMinidump(sourcePath, processId);

            Assert.True(collector.TryCopyDump(sourcePath, destinationPath));
            int copiedProcessId;
            Assert.True(MinidumpProcessIdReader.TryReadProcessId(
                destinationPath,
                out copiedProcessId));
            Assert.Equal(processId, copiedProcessId);
        }

        [Fact]
        public void IsCleanExit_TreatsMinusOneAfterEngineShutdownAsClean()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            WriteEngineLog(
                options,
                processId,
                "[23:40:13.551] Managed Interface deleted.\r\n" +
                "[23:40:13.655] \r\nError: Non-Zero Device Reference Count! (ERC4)\r\n");
            var collector = new CrashReportCollector(options);

            Assert.True(collector.IsCleanExit(-1));
        }

        [Fact]
        public void IsCleanExit_ReportsMinusOneWithoutEngineShutdown()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            WriteEngineLog(
                options,
                processId,
                "[01:00:30.298] Before Daily Tick: Summer 12, 374 \r\n");
            var collector = new CrashReportCollector(options);

            Assert.False(collector.IsCleanExit(-1));
        }

        [Fact]
        public void IsCleanExit_ReportsCrashAfterEngineShutdown()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            WriteEngineLog(
                options,
                processId,
                "[23:40:13.551] Managed Interface deleted.\r\n");
            var collector = new CrashReportCollector(options);

            Assert.False(collector.IsCleanExit(-1073741819));
        }

        [Fact]
        public void IsCleanExit_ReadsAnEngineLogShorterThanTheTail()
        {
            int processId = Process.GetCurrentProcess().Id;
            CrashReporterOptions options = CreateOptions(processId);
            string logsRoot = Path.Combine(options.BannerlordDataRoot, "logs");
            Directory.CreateDirectory(logsRoot);
            File.WriteAllText(
                Path.Combine(logsRoot, "rgl_log_" + processId + ".txt"),
                "[23:40:13.551] Managed Interface deleted.\r\n");
            var collector = new CrashReportCollector(options);

            Assert.True(collector.IsCleanExit(-1));
        }

        [Fact]
        public void IsCleanExit_ReportsMinusOneWithoutAnEngineLog()
        {
            CrashReporterOptions options = CreateOptions(Process.GetCurrentProcess().Id);
            var collector = new CrashReportCollector(options);

            Assert.False(collector.IsCleanExit(-1));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private CrashReporterOptions CreateOptions(int processId)
        {
            Directory.CreateDirectory(tempRoot);
            return new CrashReporterOptions(
                processId,
                DateTime.UtcNow.AddMinutes(-1).Ticks,
                Path.Combine(tempRoot, "coop.log"),
                "client",
                "test",
                tempRoot,
                Path.Combine(tempRoot, "reports"));
        }

        private static void WriteEngineLog(
            CrashReporterOptions options,
            int processId,
            string lastLines)
        {
            string logsRoot = Path.Combine(options.BannerlordDataRoot, "logs");
            Directory.CreateDirectory(logsRoot);
            File.WriteAllText(
                Path.Combine(logsRoot, "rgl_log_" + processId + ".txt"),
                new string('x', 10000) + "\r\n" + lastLines);
        }

        private static void WriteMinidump(string path, int processId)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x504D444D);
                writer.Write(0);
                writer.Write(1);
                writer.Write(32);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0L);

                writer.Write(15);
                writer.Write(12);
                writer.Write(44);

                writer.Write(12);
                writer.Write(1);
                writer.Write(processId);
            }
        }
    }
}
