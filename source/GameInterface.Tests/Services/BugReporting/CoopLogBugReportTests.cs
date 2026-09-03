using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.BugReporting;
using GameInterface.Services.BugReporting.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.UI.BugReporting;
using Moq;
using Serilog;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Services.BugReporting;

/// <summary>Tests diagnostic log redaction, archival, and upload configuration.</summary>
public class CoopLogBugReportTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "CoopLogBugReportTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Snapshot_RedactsSensitiveValues_AndCompressesTheCurrentLog()
    {
        Directory.CreateDirectory(tempRoot);
        var logPath = Path.Combine(tempRoot, "Coop_client.log");
        File.WriteAllLines(logPath, new[]
        {
            "ordinary diagnostic line",
            "prefix #TW#Runtime#TW#Arguments#TW# /serverpassword secret",
            "Command Args: +connect secret",
            "GET https://example.test/?access_token=secret&value=1",
            "Authorization: Bearer secret",
            "Path C:\\Users\\" + Environment.UserName + "\\Documents\\save",
        });
        var provider = new CoopLogSnapshotProvider(new CoopLogFile(logPath));

        Assert.True(provider.TryCapture(out var snapshot));
        var text = Decompress(snapshot.CompressedData);

        Assert.Contains("ordinary diagnostic line", text);
        Assert.DoesNotContain("/serverpassword secret", text);
        Assert.DoesNotContain("+connect secret", text);
        Assert.DoesNotContain("access_token=secret", text);
        Assert.DoesNotContain("Bearer secret", text);
        Assert.DoesNotContain("\\" + Environment.UserName + "\\", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), snapshot.UncompressedLength);
    }

    [Fact]
    public void Archive_ContainsManifestAndPseudonymousClientLogs()
    {
        Directory.CreateDirectory(tempRoot);
        var logBytes = Compress("client diagnostic\n");
        var serverLogBytes = Compress("server diagnostic\n");
        var serverSaveBytes = Encoding.UTF8.GetBytes("server save data");
        var serverSaveSidecarBytes = Encoding.UTF8.GetBytes("{\"players\":[]}");
        var requestId = Guid.NewGuid().ToString("N");
        using var builder = new BugReportArchiveBuilder(tempRoot);
        var contents = new BugReportArchiveContents(
            requestId,
            "network-client-1",
            new[] { "player-submitted" },
            new[] { new BugReportSubmission(
                "network-client-1",
                "Crash near Danustica",
                "Opened the town menu and crashed.") },
            new CollectedBugReportServerLog(
                serverLogBytes,
                Encoding.UTF8.GetByteCount("server diagnostic\n")),
            new CollectedBugReportServerSave(
                "coop_bug_report.sav",
                serverSaveBytes,
                "coop_bug_report.json",
                serverSaveSidecarBytes),
            DateTimeOffset.UtcNow,
            new[] { new CollectedBugReportLog(2, logBytes, Encoding.UTF8.GetByteCount("client diagnostic\n")) },
            expectedClients: 3,
            declinedClients: 1,
            failedClients: 0,
            timedOutClients: 1);

        var path = builder.Create(contents);

        using var archive = ZipFile.OpenRead(path);
        var manifestEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("manifest.txt"));
        using (var manifestReader = new StreamReader(manifestEntry.Open()))
        {
            var manifest = manifestReader.ReadToEnd();
            Assert.Contains("Reporting client network id: network-client-1", manifest);
            Assert.Contains("Module version: " + ModInformation.Version, manifest);
            Assert.Contains("Commit: " + ModInformation.Commit, manifest);
            Assert.Contains("Build version: " + ModInformation.BuildVersion, manifest);
            Assert.Contains("Triggers: player-submitted", manifest);
            Assert.Contains("Server save sidecar included: yes", manifest);
        }
        var reportEntry = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("reports/"));
        using (var reportReader = new StreamReader(reportEntry.Open()))
        {
            var report = reportReader.ReadToEnd();
            Assert.Contains("Summary: Crash near Danustica", report);
            Assert.Contains("Opened the town menu and crashed.", report);
        }
        var serverLogEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("server/server.log"));
        using (var serverLogReader = new StreamReader(serverLogEntry.Open()))
        {
            Assert.Equal("server diagnostic\n", serverLogReader.ReadToEnd());
        }
        var serverSaveEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("server/coop_bug_report.sav"));
        using (var saveData = new MemoryStream())
        {
            using var saveStream = serverSaveEntry.Open();
            saveStream.CopyTo(saveData);
            Assert.Equal(serverSaveBytes, saveData.ToArray());
        }
        var serverSaveSidecarEntry = Assert.IsType<ZipArchiveEntry>(
            archive.GetEntry("server/coop_bug_report.json"));
        using (var sidecarData = new MemoryStream())
        {
            using var sidecarStream = serverSaveSidecarEntry.Open();
            sidecarStream.CopyTo(sidecarData);
            Assert.Equal(serverSaveSidecarBytes, sidecarData.ToArray());
        }
        var logEntry = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("clients/"));
        Assert.Equal("clients/client-02.log", logEntry.FullName);
        using var reader = new StreamReader(logEntry.Open());
        Assert.Equal("client diagnostic\n", reader.ReadToEnd());
    }

    [Fact]
    public void LogValidator_KeepsValidLogAndRejectsInvalidGzip()
    {
        var valid = new CollectedBugReportLog(
            1,
            Compress("valid diagnostic\n"),
            Encoding.UTF8.GetByteCount("valid diagnostic\n"));
        var invalid = new CollectedBugReportLog(2, new byte[] { 1, 2, 3, 4 }, 12);
        var validator = new BugReportLogValidator();

        var result = validator.Validate(new[] { valid, invalid });

        Assert.Equal(1, result.InvalidCount);
        Assert.Same(valid, Assert.Single(result.ValidLogs));
    }

    [Fact]
    public void CollectionBuffer_ReservesAggregateLimitBeforeAllocatingClientLog()
    {
        Assert.True(BugReportService.CanReserveCompressedBytes(
            BugReportService.MaximumCombinedCompressedBytes - 1,
            1));
        Assert.False(BugReportService.CanReserveCompressedBytes(
            BugReportService.MaximumCombinedCompressedBytes,
            1));
        Assert.Equal(4 * 1024, NetworkBugReportLogChunk.ChunkSize);
    }

    [Fact]
    public void PrepareContents_WithNoClientLogs_StillCapturesServerLog()
    {
        var compressed = Compress("server diagnostic\n");
        var snapshot = new CoopLogSnapshot(
            compressed,
            Encoding.UTF8.GetByteCount("server diagnostic\n"));
        var snapshotProvider = new Mock<ICoopLogSnapshotProvider>();
        snapshotProvider.Setup(value => value.TryCapture(out snapshot)).Returns(true);
        using var cancellation = new CancellationTokenSource();
        using var service = new BugReportService(
            Mock.Of<IMessageBroker>(),
            Mock.Of<INetwork>(),
            Mock.Of<IPlayerManager>(),
            Mock.Of<IBugReportLogSharingPreference>(),
            snapshotProvider.Object,
            Mock.Of<IBugReportServerSaveProvider>(),
            Mock.Of<IBugReportArchiveBuilder>(),
            new BugReportLogValidator(),
            Mock.Of<IBugReportUploader>(),
            cancellation);

        var contents = service.PrepareContents(CreateEmptyArchiveContents());

        Assert.NotNull(contents.ServerLog);
        Assert.Equal(compressed, contents.ServerLog.CompressedData);
    }

    [Fact]
    public void ServerSaveProvider_PersistsAndReturnsTheCampaignSave()
    {
        var saveData = Encoding.UTF8.GetBytes("campaign save");
        var sidecarData = Encoding.UTF8.GetBytes("{\"players\":[]}");
        var saveInterface = new Mock<ISaveInterface>();
        saveInterface
            .Setup(value => value.SaveCurrentGameToFile(BugReportServerSaveProvider.SaveName))
            .Returns(new SaveResults(true, saveData, "campaign-id"));
        saveInterface
            .Setup(value => value.ReadSaveFile("coop_bug_report.json"))
            .Returns(sidecarData);
        var provider = new BugReportServerSaveProvider(saveInterface.Object, Mock.Of<Serilog.ILogger>());

        var captured = provider.TryCapture(out var save);

        Assert.True(captured);
        Assert.Equal("coop_bug_report.sav", save.FileName);
        Assert.Equal(saveData, save.Data);
        Assert.Equal("coop_bug_report.json", save.SidecarFileName);
        Assert.Equal(sidecarData, save.SidecarData);
        saveInterface.Verify(
            value => value.SaveCurrentGameToFile(BugReportServerSaveProvider.SaveName),
            Times.Once);
    }

    [Fact]
    public void ServerSaveProvider_MissingSidecarStillReturnsCampaignSave()
    {
        var saveData = Encoding.UTF8.GetBytes("campaign save");
        var saveInterface = new Mock<ISaveInterface>();
        saveInterface
            .Setup(value => value.SaveCurrentGameToFile(BugReportServerSaveProvider.SaveName))
            .Returns(new SaveResults(true, saveData, "campaign-id"));
        using var logger = new LoggerConfiguration().CreateLogger();
        var provider = new BugReportServerSaveProvider(saveInterface.Object, logger);

        var captured = provider.TryCapture(out var save);

        Assert.True(captured);
        Assert.Equal(saveData, save.Data);
        Assert.Null(save.SidecarFileName);
        Assert.Null(save.SidecarData);
    }

    [Fact]
    public void Archive_DeletesOldestPendingReportWhenCountQuotaIsReached()
    {
        Directory.CreateDirectory(tempRoot);
        using var builder = new BugReportArchiveBuilder(
            tempRoot,
            maximumPendingArchiveCount: 2,
            maximumPendingArchiveBytes: long.MaxValue);
        var first = builder.Create(CreateEmptyArchiveContents());
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddMinutes(-2));
        var second = builder.Create(CreateEmptyArchiveContents());
        File.SetLastWriteTimeUtc(second, DateTime.UtcNow.AddMinutes(-1));

        var third = builder.Create(CreateEmptyArchiveContents());

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.True(File.Exists(third));
        Assert.Equal(2, Directory.GetFiles(tempRoot, "bug_report_*.zip").Length);
    }

    [Fact]
    public void Archive_SkipsLockedOldestReportWhenEnforcingQuota()
    {
        Directory.CreateDirectory(tempRoot);
        string lockedPath = null;
        bool DeleteUnlessLocked(string path)
        {
            if (string.Equals(path, lockedPath, StringComparison.OrdinalIgnoreCase)) return false;
            File.Delete(path);
            return true;
        }

        using var builder = new BugReportArchiveBuilder(
            tempRoot,
            maximumPendingArchiveCount: 2,
            maximumPendingArchiveBytes: long.MaxValue,
            deleteArchive: DeleteUnlessLocked);
        lockedPath = builder.Create(CreateEmptyArchiveContents());
        File.SetLastWriteTimeUtc(lockedPath, DateTime.UtcNow.AddMinutes(-2));
        var removablePath = builder.Create(CreateEmptyArchiveContents());
        File.SetLastWriteTimeUtc(removablePath, DateTime.UtcNow.AddMinutes(-1));

        var newPath = builder.Create(CreateEmptyArchiveContents());

        Assert.True(File.Exists(lockedPath));
        Assert.False(File.Exists(removablePath));
        Assert.True(File.Exists(newPath));
        Assert.Equal(2, Directory.GetFiles(tempRoot, "bug_report_*.zip").Length);
    }

    [Fact]
    public void Archive_RejectsReportLargerThanPendingByteQuota()
    {
        Directory.CreateDirectory(tempRoot);
        using var builder = new BugReportArchiveBuilder(
            tempRoot,
            maximumPendingArchiveCount: 2,
            maximumPendingArchiveBytes: 1);

        Assert.Throws<InvalidDataException>(() => builder.Create(CreateEmptyArchiveContents()));
        Assert.Empty(Directory.GetFiles(tempRoot, "bug_report_*.zip"));
    }

    [Fact]
    public void Archive_PeriodicallyDeletesExpiredReportWithoutAnotherCreate()
    {
        Directory.CreateDirectory(tempRoot);
        using var builder = new BugReportArchiveBuilder(
            tempRoot,
            pendingArchiveRetention: TimeSpan.FromMilliseconds(20),
            cleanupInterval: TimeSpan.FromMilliseconds(20));
        var path = builder.Create(CreateEmptyArchiveContents());
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));

        Assert.True(SpinWait.SpinUntil(() => !File.Exists(path), TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Archive_LockedExpiredReportDoesNotBlockOtherCleanup()
    {
        Directory.CreateDirectory(tempRoot);
        var lockedPath = Path.Combine(tempRoot, "bug_report_locked.zip");
        var removablePath = Path.Combine(tempRoot, "bug_report_removable.zip");
        File.WriteAllText(lockedPath, "locked");
        File.WriteAllText(removablePath, "removable");
        File.SetLastWriteTimeUtc(lockedPath, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(removablePath, DateTime.UtcNow.AddDays(-2));

        using (new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
        using (var builder = new BugReportArchiveBuilder(
                   tempRoot,
                   pendingArchiveRetention: TimeSpan.FromDays(1)))
        {
            if (OperatingSystem.IsWindows()) Assert.True(File.Exists(lockedPath));
            Assert.False(File.Exists(removablePath));
        }
    }

    [Fact]
    public async Task Uploader_PutsMetadataLogsAndServerSaveInOneMultipartRequest()
    {
        var handler = new RecordingHttpHandler();
        using var httpClient = new HttpClient(handler);
        using var uploader = new BugReportUploader(
            httpClient,
            "https://bug-reports.example.test/api/v1/reports");
        var serverLog = Compress("server diagnostic\n");
        var clientLog = Compress("client diagnostic\n");
        var serverSave = Encoding.UTF8.GetBytes("server campaign save");
        var serverSaveSidecar = Encoding.UTF8.GetBytes("{\"players\":[]}");
        var report = new BugReportArchiveContents(
            Guid.NewGuid().ToString("N"),
            "network-client-1",
            new[] { "player-submitted" },
            new[] { new BugReportSubmission(
                "network-client-1",
                "Cannot leave Danustica",
                "Leaving Danustica keeps reopening the town menu.") },
            new CollectedBugReportServerLog(
                serverLog,
                Encoding.UTF8.GetByteCount("server diagnostic\n")),
            new CollectedBugReportServerSave(
                "coop_bug_report.sav",
                serverSave,
                "coop_bug_report.json",
                serverSaveSidecar),
            DateTimeOffset.UtcNow,
            new[] { new CollectedBugReportLog(
                1,
                clientLog,
                Encoding.UTF8.GetByteCount("client diagnostic\n")) },
            expectedClients: 2,
            declinedClients: 1,
            failedClients: 0,
            timedOutClients: 0);

        var result = await uploader.UploadAsync(report, CancellationToken.None);

        Assert.True(result.Uploaded);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.StartsWith("multipart/form-data; boundary=", handler.ContentType);
        Assert.Null(handler.ApiKey);
        Assert.Null(handler.Authorization);
        Assert.Equal(report.RequestId, handler.IdempotencyKey);
        Assert.Equal(serverLog, handler.ServerLogBody);
        Assert.Equal(clientLog, handler.ClientLogBody);
        Assert.Equal(serverSave, handler.ServerSaveBody);
        Assert.Equal(serverSaveSidecar, handler.ServerSaveSidecarBody);
        using var json = JsonDocument.Parse(handler.Body);
        var root = json.RootElement;
        Assert.Equal("network-client-1", root.GetProperty("reportingClientNetworkId").GetString());
        Assert.Equal("Cannot leave Danustica", root.GetProperty("summary").GetString());
        Assert.Equal(ModInformation.Version.ToString(), root.GetProperty("moduleVersion").GetString());
        Assert.Equal(ModInformation.Commit, root.GetProperty("commit").GetString());
        Assert.Equal(ModInformation.BuildVersion, root.GetProperty("buildVersion").GetString());
        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("gzip", root.GetProperty("serverLog").GetProperty("contentEncoding").GetString());
        Assert.Equal(serverLog.Length, root.GetProperty("serverLog").GetProperty("compressedLength").GetInt32());
        Assert.False(root.GetProperty("serverLog").TryGetProperty("data", out _));
        Assert.Equal("gzip", root.GetProperty("clientLogs")[0].GetProperty("contentEncoding").GetString());
        Assert.Equal(clientLog.Length, root.GetProperty("clientLogs")[0].GetProperty("compressedLength").GetInt32());
        Assert.False(root.GetProperty("clientLogs")[0].TryGetProperty("data", out _));
        Assert.Equal("server-save", root.GetProperty("serverSave").GetProperty("artifact").GetString());
        Assert.Equal(serverSave.Length, root.GetProperty("serverSave").GetProperty("length").GetInt64());
        Assert.Equal(
            "coop_bug_report.json",
            root.GetProperty("serverSave").GetProperty("sidecarFileName").GetString());
        Assert.Equal(
            serverSaveSidecar.Length,
            root.GetProperty("serverSave").GetProperty("sidecarLength").GetInt64());
    }

    [Fact]
    public async Task Uploader_ReturnsFailureWhenMultipartPutFails()
    {
        var handler = new RecordingHttpHandler { FailUpload = true };
        using var httpClient = new HttpClient(handler);
        using var uploader = new BugReportUploader(
            httpClient,
            "https://bug-reports.example.test/api/v1/reports");
        var report = new BugReportArchiveContents(
            Guid.NewGuid().ToString("N"),
            "network-client-1",
            Array.Empty<string>(),
            Array.Empty<BugReportSubmission>(),
            null,
            new CollectedBugReportServerSave(
                "coop_bug_report.sav",
                Encoding.UTF8.GetBytes("server campaign save")),
            DateTimeOffset.UtcNow,
            Array.Empty<CollectedBugReportLog>(),
            expectedClients: 0,
            declinedClients: 0,
            failedClients: 0,
            timedOutClients: 0);

        var result = await uploader.UploadAsync(report, CancellationToken.None);

        Assert.False(result.Uploaded);
        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(handler.Body);
        Assert.NotNull(handler.ServerSaveBody);
    }

    [Fact]
    public void Uploader_DefaultConfigurationUsesPublicEdgeFunction()
    {
        using var httpClient = new HttpClient(new RecordingHttpHandler());
        using var uploader = new BugReportUploader(httpClient);

        Assert.True(uploader.IsConfigured);
        Assert.Equal(
            "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/create-github-issue-bug-report",
            BugReportUploader.Endpoint);
    }

    [Fact]
    public void Uploader_AnonymousEndpointDoesNotRequireCredentials()
    {
        using var httpClient = new HttpClient(new RecordingHttpHandler());
        using var uploader = new BugReportUploader(
            httpClient,
            "https://bug-reports.example.test/api/v1/reports");

        Assert.True(uploader.IsConfigured);
    }

    [Fact]
    public void Uploader_EnforcesAttachmentLimitsBeforeSending()
    {
        Assert.True(BugReportUploader.IsWithinCompressedLogLimit(
            BugReportUploader.MaximumCompressedReportBytes));
        Assert.False(BugReportUploader.IsWithinCompressedLogLimit(
            (long)BugReportUploader.MaximumCompressedReportBytes + 1));
        Assert.True(BugReportUploader.IsWithinServerSaveLimit(
            BugReportUploader.MaximumServerSaveBytes));
        Assert.False(BugReportUploader.IsWithinServerSaveLimit(
            (long)BugReportUploader.MaximumServerSaveBytes + 1));
        Assert.True(BugReportUploader.IsWithinServerSaveSidecarLimit(
            BugReportUploader.MaximumServerSaveSidecarBytes));
        Assert.False(BugReportUploader.IsWithinServerSaveSidecarLimit(
            (long)BugReportUploader.MaximumServerSaveSidecarBytes + 1));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static BugReportArchiveContents CreateEmptyArchiveContents()
    {
        return new BugReportArchiveContents(
            Guid.NewGuid().ToString("N"),
            "network-client-1",
            Array.Empty<string>(),
            Array.Empty<BugReportSubmission>(),
            null,
            null,
            DateTimeOffset.UtcNow,
            Array.Empty<CollectedBugReportLog>(),
            expectedClients: 0,
            declinedClients: 0,
            failedClients: 0,
            timedOutClients: 0);
    }

    private static byte[] Compress(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write(text);
        }
        return output.ToArray();
    }

    private static string Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod Method { get; private set; }
        public string Body { get; private set; }
        public string ContentType { get; private set; }
        public string ApiKey { get; private set; }
        public string Authorization { get; private set; }
        public string IdempotencyKey { get; private set; }
        public byte[] ServerLogBody { get; private set; }
        public byte[] ClientLogBody { get; private set; }
        public byte[] ServerSaveBody { get; private set; }
        public byte[] ServerSaveSidecarBody { get; private set; }
        public bool FailUpload { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method;
            ContentType = request.Content.Headers.ContentType.ToString();
            ApiKey = request.Headers.TryGetValues("apikey", out var apiKeys)
                ? string.Join(string.Empty, apiKeys)
                : null;
            Authorization = request.Headers.Authorization?.ToString();
            IdempotencyKey = string.Join(string.Empty, request.Headers.GetValues("Idempotency-Key"));

            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition.Name.Trim('"');
                if (name == "report") Body = await part.ReadAsStringAsync();
                else if (name == "serverLog") ServerLogBody = await part.ReadAsByteArrayAsync();
                else if (name == "serverSave") ServerSaveBody = await part.ReadAsByteArrayAsync();
                else if (name == "serverSaveSidecar")
                    ServerSaveSidecarBody = await part.ReadAsByteArrayAsync();
                else if (name == BugReportUploader.GetClientLogPartName(1))
                    ClientLogBody = await part.ReadAsByteArrayAsync();
            }

            return new HttpResponseMessage(FailUpload ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(FailUpload ? "report rejected" : "accepted"),
            };
        }
    }
}
