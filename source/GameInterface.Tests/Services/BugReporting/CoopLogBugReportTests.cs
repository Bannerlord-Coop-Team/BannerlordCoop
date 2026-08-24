using Common;
using Common.Logging;
using GameInterface.Services.BugReporting;
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
        var requestId = Guid.NewGuid().ToString("N");
        var builder = new BugReportArchiveBuilder(tempRoot);
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
        var logEntry = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("clients/"));
        Assert.Equal("clients/client-02.log", logEntry.FullName);
        using var reader = new StreamReader(logEntry.Open());
        Assert.Equal("client diagnostic\n", reader.ReadToEnd());
    }

    [Fact]
    public async Task Uploader_PostsJsonWithReporterServerLogAndClientLogs()
    {
        var handler = new RecordingHttpHandler();
        using var httpClient = new HttpClient(handler);
        using var uploader = new BugReportUploader(
            httpClient,
            "https://bug-reports.example.test/api/v1/reports");
        var serverLog = Compress("server diagnostic\n");
        var clientLog = Compress("client diagnostic\n");
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
        Assert.Equal("application/json; charset=utf-8", handler.ContentType);
        Assert.Equal(BugReportUploader.SupabasePublishableKey, handler.ApiKey);
        Assert.Equal("Bearer " + BugReportUploader.SupabasePublishableKey, handler.Authorization);
        using var json = JsonDocument.Parse(handler.Body);
        var root = json.RootElement;
        Assert.Equal("network-client-1", root.GetProperty("reportingClientNetworkId").GetString());
        Assert.Equal("Cannot leave Danustica", root.GetProperty("summary").GetString());
        Assert.Equal(ModInformation.Version.ToString(), root.GetProperty("moduleVersion").GetString());
        Assert.Equal(ModInformation.Commit, root.GetProperty("commit").GetString());
        Assert.Equal(ModInformation.BuildVersion, root.GetProperty("buildVersion").GetString());
        Assert.Equal(
            Convert.ToBase64String(serverLog),
            root.GetProperty("serverLog").GetProperty("data").GetString());
        Assert.Equal(
            Convert.ToBase64String(clientLog),
            root.GetProperty("clientLogs")[0].GetProperty("data").GetString());
    }

    [Fact]
    public void Uploader_UsesConfiguredSupabaseEndpoint()
    {
        using var uploader = new BugReportUploader();

        Assert.True(uploader.IsConfigured);
        Assert.Equal(
            "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/create-github-issue-bug-report",
            BugReportUploader.Endpoint);
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
        public string Body { get; private set; }
        public string ContentType { get; private set; }
        public string ApiKey { get; private set; }
        public string Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ContentType = request.Content.Headers.ContentType.ToString();
            ApiKey = string.Join(string.Empty, request.Headers.GetValues("apikey"));
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("accepted"),
            };
        }
    }
}
