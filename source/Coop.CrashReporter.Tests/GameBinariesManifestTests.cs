using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Coop.CrashReporter.Tests
{
    public sealed class GameBinariesManifestTests : IDisposable
    {
        private readonly string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "CoopBinariesManifestTests_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void Write_ListsFilesWithSizesAndVersions()
        {
            string binariesDirectory = CreateBinariesDirectory();
            File.WriteAllText(Path.Combine(binariesDirectory, "engine_config.txt"), "abc");
            string assemblyPath = Path.Combine(binariesDirectory, "Sample.dll");
            File.Copy(typeof(GameBinariesManifest).Assembly.Location, assemblyPath);
            string expectedVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;
            Assert.False(string.IsNullOrWhiteSpace(expectedVersion));

            bool written;
            string manifestPath = Path.Combine(tempRoot, GameBinariesManifest.FileName);
            string summary = GameBinariesManifest.Write(
                manifestPath,
                binariesDirectory,
                out written);

            Assert.True(written);
            Assert.Equal("2 files listed in " + GameBinariesManifest.FileName, summary);
            string manifest = File.ReadAllText(manifestPath);
            Assert.Contains("\"fileCount\": 2", manifest);
            Assert.Contains("\"truncated\": false", manifest);
            Assert.Contains("\"path\": \"engine_config.txt\", \"bytes\": 3", manifest);
            Assert.Contains("\"path\": \"Sample.dll\"", manifest);
            Assert.Contains("\"fileVersion\": \"" + expectedVersion.Trim() + "\"", manifest);
        }

        [Fact]
        public void Write_ListsNestedFilesRelativeToTheBinariesDirectory()
        {
            string binariesDirectory = CreateBinariesDirectory();
            Directory.CreateDirectory(Path.Combine(binariesDirectory, "Watchdog"));
            File.WriteAllText(
                Path.Combine(binariesDirectory, "Watchdog", "Watchdog.txt"),
                "watchdog");

            bool written;
            string manifestPath = Path.Combine(tempRoot, GameBinariesManifest.FileName);
            GameBinariesManifest.Write(manifestPath, binariesDirectory, out written);

            Assert.True(written);
            Assert.Contains("\"path\": \"Watchdog/Watchdog.txt\"", File.ReadAllText(manifestPath));
        }

        [Fact]
        public void Write_SkipsAndCountsDirectoriesBeyondTheDepthLimit()
        {
            string binariesDirectory = CreateBinariesDirectory();
            string deepDirectory = binariesDirectory;
            for (var depth = 1; depth <= GameBinariesManifest.MaximumDirectoryDepth + 1; depth++)
            {
                deepDirectory = Path.Combine(deepDirectory, "level" + depth);
                Directory.CreateDirectory(deepDirectory);
                File.WriteAllText(Path.Combine(deepDirectory, "level" + depth + ".txt"), "x");
            }

            bool written;
            string manifestPath = Path.Combine(tempRoot, GameBinariesManifest.FileName);
            string summary = GameBinariesManifest.Write(
                manifestPath,
                binariesDirectory,
                out written);

            string manifest = File.ReadAllText(manifestPath);
            Assert.True(written);
            Assert.Contains("\"skippedDirectories\": 1", manifest);
            Assert.Contains("\"path\": \"level1/level1.txt\"", manifest);
            Assert.Contains("\"path\": \"level1/level2/level2.txt\"", manifest);
            Assert.DoesNotContain("level3.txt", manifest);
            Assert.Contains("1 deeper directories not listed", summary);
        }

        [Fact]
        public void Write_EscapesTheBinariesDirectoryPath()
        {
            string binariesDirectory = CreateBinariesDirectory();

            bool written;
            string manifestPath = Path.Combine(tempRoot, GameBinariesManifest.FileName);
            GameBinariesManifest.Write(manifestPath, binariesDirectory, out written);

            Assert.Contains(
                "\"directory\": \"" + binariesDirectory.Replace("\\", "\\\\") + "\"",
                File.ReadAllText(manifestPath));
        }

        [Fact]
        public void Write_ReportsMissingDirectoryWithoutWritingAManifest()
        {
            Directory.CreateDirectory(tempRoot);
            string missingDirectory = Path.Combine(tempRoot, "absent");

            bool written;
            string manifestPath = Path.Combine(tempRoot, GameBinariesManifest.FileName);
            string summary = GameBinariesManifest.Write(
                manifestPath,
                missingDirectory,
                out written);

            Assert.False(written);
            Assert.False(File.Exists(manifestPath));
            Assert.Contains("not captured", summary);
            Assert.Contains(missingDirectory, summary);
        }

        [Fact]
        public void Write_ReportsUnresolvedDirectoryWithoutWritingAManifest()
        {
            Directory.CreateDirectory(tempRoot);

            bool written;
            string manifestPath = Path.Combine(tempRoot, GameBinariesManifest.FileName);
            string summary = GameBinariesManifest.Write(manifestPath, null, out written);

            Assert.False(written);
            Assert.False(File.Exists(manifestPath));
            Assert.Equal("not captured (directory was not resolved)", summary);
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

        private string CreateBinariesDirectory()
        {
            string binariesDirectory = Path.Combine(tempRoot, "Win64_Shipping_Client");
            Directory.CreateDirectory(binariesDirectory);
            return binariesDirectory;
        }
    }
}
