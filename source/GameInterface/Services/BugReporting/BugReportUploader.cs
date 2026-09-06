using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameInterface.Services.BugReporting;

/// <summary>Uploads diagnostic metadata, logs, and the server save in one multipart request.</summary>
public interface IBugReportUploader
{
    bool IsConfigured { get; }
    Task<BugReportUploadResult> UploadAsync(
        BugReportArchiveContents report,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
public class BugReportUploader : IBugReportUploader, IDisposable
{
    public const string Endpoint =
        "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/create-github-issue-bug-report";
    internal const int MaximumCompressedReportBytes = 10 * 1024 * 1024;
    internal const int MaximumServerSaveBytes = 48 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient httpClient;
    private readonly string endpoint;
    private readonly bool ownsClient;

    public bool IsConfigured =>
        !new Uri(endpoint).Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);

    public BugReportUploader() : this(
        new HttpClient { Timeout = TimeSpan.FromMinutes(2) },
        Endpoint,
        true)
    {
    }

    internal BugReportUploader(
        HttpClient httpClient,
        string endpoint = Endpoint,
        bool ownsClient = false)
    {
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty.", nameof(endpoint));
        this.httpClient = httpClient;
        this.endpoint = endpoint;
        this.ownsClient = ownsClient;
    }

    public async Task<BugReportUploadResult> UploadAsync(
        BugReportArchiveContents report,
        CancellationToken cancellationToken)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (!IsConfigured)
        {
            return new BugReportUploadResult(
                false,
                false,
                "The bug-report upload endpoint is not configured.");
        }

        if (!IsWithinCompressedLogLimit(GetCompressedLogBytes(report)))
        {
            return new BugReportUploadResult(
                false,
                true,
                "The bug report logs exceed the upload size limit.");
        }

        if (report.ServerSave != null)
        {
            if (string.IsNullOrWhiteSpace(report.ServerSave.FileName) ||
                !string.Equals(
                    Path.GetFileName(report.ServerSave.FileName),
                    report.ServerSave.FileName,
                    StringComparison.Ordinal))
            {
                return new BugReportUploadResult(false, true, "The server campaign save had an invalid file name.");
            }

            if (!IsWithinServerSaveLimit(report.ServerSave.Data?.LongLength ?? 0))
            {
                return new BugReportUploadResult(
                    false,
                    true,
                    "The server campaign save exceeds the upload size limit.");
            }
        }

        var json = JsonSerializer.Serialize(CreateRequest(report), JsonOptions);
        using var content = CreateMultipartContent(report, json);
        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = content,
        };
        AddRequestHeaders(request, report.RequestId);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var details = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new BugReportUploadResult(
                false,
                true,
                string.IsNullOrWhiteSpace(details)
                    ? "The bug-report endpoint returned " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + "."
                    : details);
        }

        return new BugReportUploadResult(true, true, details);
    }

    public void Dispose()
    {
        if (ownsClient) httpClient.Dispose();
    }

    internal static bool IsWithinCompressedLogLimit(long compressedBytes)
    {
        return compressedBytes >= 0 && compressedBytes <= MaximumCompressedReportBytes;
    }

    internal static bool IsWithinServerSaveLimit(long saveBytes)
    {
        return saveBytes > 0 && saveBytes <= MaximumServerSaveBytes;
    }

    private static MultipartFormDataContent CreateMultipartContent(
        BugReportArchiveContents report,
        string json)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(json, Encoding.UTF8, "application/json"), "report");

        if (report.ServerLog != null)
        {
            content.Add(
                CreateBinaryContent(report.ServerLog.CompressedData, "application/gzip"),
                "serverLog",
                "Coop_server.log.gz");
        }

        foreach (var log in report.Logs.OrderBy(item => item.ClientNumber))
        {
            content.Add(
                CreateBinaryContent(log.CompressedData, "application/gzip"),
                GetClientLogPartName(log.ClientNumber),
                "client-" + log.ClientNumber.ToString("D2", CultureInfo.InvariantCulture) + ".log.gz");
        }

        if (report.ServerSave != null)
        {
            content.Add(
                CreateBinaryContent(report.ServerSave.Data, "application/octet-stream"),
                "serverSave",
                report.ServerSave.FileName);
        }

        return content;
    }

    private static ByteArrayContent CreateBinaryContent(byte[] data, string mediaType)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return content;
    }

    internal static string GetClientLogPartName(int clientNumber)
    {
        return "clientLog-" + clientNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static void AddRequestHeaders(HttpRequestMessage request, string idempotencyKey)
    {
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
    }

    private static long GetCompressedLogBytes(BugReportArchiveContents report)
    {
        var bytes = report.ServerLog?.CompressedData?.LongLength ?? 0;
        return report.Logs.Aggregate(
            bytes,
            (total, log) => total + (log?.CompressedData?.LongLength ?? 0));
    }

    private static BugReportJsonRequest CreateRequest(BugReportArchiveContents report)
    {
        var primarySubmission = report.Submissions.FirstOrDefault(submission =>
                                    submission.ReportingClientNetworkId == report.ReportingClientNetworkId) ??
                                report.Submissions.FirstOrDefault();
        var serverLog = report.ServerLog == null
            ? null
            : new BugReportJsonLog(
                "Coop_server.log",
                report.ServerLog.UncompressedLength,
                report.ServerLog.CompressedData);
        var clientLogs = report.Logs
            .OrderBy(log => log.ClientNumber)
            .Select(log => new BugReportJsonClientLog(
                log.ClientNumber,
                log.UncompressedLength,
                log.CompressedData))
            .ToArray();
        var submissions = report.Submissions
            .Select(submission => new BugReportJsonSubmission(
                submission.ReportingClientNetworkId,
                submission.Summary,
                submission.Description))
            .ToArray();
        var serverSave = report.ServerSave == null
            ? null
            : new BugReportJsonServerSave(
                report.ServerSave.FileName,
                report.ServerSave.Data.LongLength);

        return new BugReportJsonRequest(
            report.RequestId,
            report.ReportingClientNetworkId,
            primarySubmission?.Summary ?? string.Empty,
            primarySubmission?.Description ?? string.Empty,
            report.Triggers,
            report.StartedAt,
            DateTimeOffset.UtcNow,
            ModInformation.Version.ToString(),
            ModInformation.Commit,
            ModInformation.BuildVersion,
            serverLog,
            serverSave,
            clientLogs,
            submissions,
            report.ExpectedClients,
            report.DeclinedClients,
            report.FailedClients,
            report.TimedOutClients);
    }
}

/// <summary>Reports whether a diagnostic bug-report upload completed.</summary>
public sealed class BugReportUploadResult
{
    public bool Uploaded { get; }
    public bool EndpointConfigured { get; }
    public string Details { get; }

    public BugReportUploadResult(bool uploaded, bool endpointConfigured, string details)
    {
        Uploaded = uploaded;
        EndpointConfigured = endpointConfigured;
        Details = details;
    }
}

/// <summary>Defines the versioned JSON payload posted to the bug-report endpoint.</summary>
internal sealed class BugReportJsonRequest
{
    public int SchemaVersion => 3;
    public string ReportId { get; }
    public string ReportingClientNetworkId { get; }
    public string Summary { get; }
    public string Description { get; }
    public IReadOnlyCollection<string> Triggers { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset PackagedAtUtc { get; }
    public string ModuleVersion { get; }
    public string Commit { get; }
    public string BuildVersion { get; }
    public BugReportJsonLog ServerLog { get; }
    public BugReportJsonServerSave ServerSave { get; }
    public IReadOnlyCollection<BugReportJsonClientLog> ClientLogs { get; }
    public IReadOnlyCollection<BugReportJsonSubmission> Submissions { get; }
    public int ExpectedClients { get; }
    public int DeclinedClients { get; }
    public int FailedClients { get; }
    public int TimedOutClients { get; }

    public BugReportJsonRequest(
        string reportId,
        string reportingClientNetworkId,
        string summary,
        string description,
        IReadOnlyCollection<string> triggers,
        DateTimeOffset startedAtUtc,
        DateTimeOffset packagedAtUtc,
        string moduleVersion,
        string commit,
        string buildVersion,
        BugReportJsonLog serverLog,
        BugReportJsonServerSave serverSave,
        IReadOnlyCollection<BugReportJsonClientLog> clientLogs,
        IReadOnlyCollection<BugReportJsonSubmission> submissions,
        int expectedClients,
        int declinedClients,
        int failedClients,
        int timedOutClients)
    {
        ReportId = reportId;
        ReportingClientNetworkId = reportingClientNetworkId;
        Summary = summary;
        Description = description;
        Triggers = triggers;
        StartedAtUtc = startedAtUtc;
        PackagedAtUtc = packagedAtUtc;
        ModuleVersion = moduleVersion;
        Commit = commit;
        BuildVersion = buildVersion;
        ServerLog = serverLog;
        ServerSave = serverSave;
        ClientLogs = clientLogs;
        Submissions = submissions;
        ExpectedClients = expectedClients;
        DeclinedClients = declinedClients;
        FailedClients = failedClients;
        TimedOutClients = timedOutClients;
    }
}

/// <summary>Describes one gzip-compressed log included as a multipart field.</summary>
internal sealed class BugReportJsonLog
{
    public string FileName { get; }
    public string ContentEncoding => "gzip";
    public int CompressedLength { get; }
    public int UncompressedLength { get; }

    public BugReportJsonLog(string fileName, int uncompressedLength, byte[] data)
    {
        FileName = fileName;
        CompressedLength = data?.Length ?? 0;
        UncompressedLength = uncompressedLength;
    }
}

/// <summary>Describes the raw server-save artifact uploaded before the JSON report.</summary>
internal sealed class BugReportJsonServerSave
{
    public string FileName { get; }
    public string Artifact => "server-save";
    public long Length { get; }

    public BugReportJsonServerSave(string fileName, long length)
    {
        FileName = fileName;
        Length = length;
    }
}

/// <summary>Describes one pseudonymous client log included as a multipart field.</summary>
internal sealed class BugReportJsonClientLog
{
    public int ClientNumber { get; }
    public string FileName => "client-" + ClientNumber.ToString("D2", CultureInfo.InvariantCulture) + ".log";
    public string ContentEncoding => "gzip";
    public int CompressedLength { get; }
    public int UncompressedLength { get; }

    public BugReportJsonClientLog(int clientNumber, int uncompressedLength, byte[] data)
    {
        ClientNumber = clientNumber;
        CompressedLength = data?.Length ?? 0;
        UncompressedLength = uncompressedLength;
    }
}

/// <summary>Contains one player-written bug report in a combined collection.</summary>
internal sealed class BugReportJsonSubmission
{
    public string ReportingClientNetworkId { get; }
    public string Summary { get; }
    public string Description { get; }

    public BugReportJsonSubmission(
        string reportingClientNetworkId,
        string summary,
        string description)
    {
        ReportingClientNetworkId = reportingClientNetworkId;
        Summary = summary;
        Description = description;
    }
}
