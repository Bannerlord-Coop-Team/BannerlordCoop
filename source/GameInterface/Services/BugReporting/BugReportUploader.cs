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

/// <summary>Uploads the server save as a raw artifact, then posts diagnostic report metadata as JSON.</summary>
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
    public const string Endpoint = "https://bug-reports.bannerlordcoop.invalid/api/v1/reports";
    public const string EndpointEnvironmentVariable = "BANNERLORDCOOP_BUG_REPORT_ENDPOINT";
    public const string PublishableKeyEnvironmentVariable = "BANNERLORDCOOP_BUG_REPORT_PUBLISHABLE_KEY";
    public const string AuthorizationTokenEnvironmentVariable = "BANNERLORDCOOP_BUG_REPORT_TOKEN";
    internal const int MaximumCompressedReportBytes = 10 * 1024 * 1024;
    internal const int MaximumServerSaveBytes = 48 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient httpClient;
    private readonly string endpoint;
    private readonly string supabasePublishableKey;
    private readonly string authorizationToken;
    private readonly bool ownsClient;

    public bool IsConfigured =>
        !new Uri(endpoint).Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(supabasePublishableKey) &&
        !string.IsNullOrWhiteSpace(authorizationToken) &&
        !string.Equals(authorizationToken, supabasePublishableKey, StringComparison.Ordinal);

    public BugReportUploader() : this(
        new HttpClient { Timeout = TimeSpan.FromMinutes(2) },
        Environment.GetEnvironmentVariable(EndpointEnvironmentVariable) ?? Endpoint,
        Environment.GetEnvironmentVariable(PublishableKeyEnvironmentVariable),
        Environment.GetEnvironmentVariable(AuthorizationTokenEnvironmentVariable),
        true)
    {
    }

    internal BugReportUploader(
        HttpClient httpClient,
        string endpoint = Endpoint,
        string supabasePublishableKey = null,
        string authorizationToken = null,
        bool ownsClient = false)
    {
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty.", nameof(endpoint));
        this.httpClient = httpClient;
        this.endpoint = endpoint;
        this.supabasePublishableKey = supabasePublishableKey;
        this.authorizationToken = authorizationToken;
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
                "Bug-report upload authorization is not configured.");
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

            var saveUpload = await UploadServerSaveAsync(report, cancellationToken).ConfigureAwait(false);
            if (!saveUpload.Uploaded) return saveUpload;
        }

        var json = JsonSerializer.Serialize(CreateRequest(report), JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
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

    private async Task<BugReportUploadResult> UploadServerSaveAsync(
        BugReportArchiveContents report,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = new ByteArrayContent(report.ServerSave.Data),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/octet-stream");
        request.Headers.TryAddWithoutValidation("X-Bug-Report-Artifact", "server-save");
        request.Headers.TryAddWithoutValidation("X-Bug-Report-Id", report.RequestId);
        request.Headers.TryAddWithoutValidation("X-Bug-Report-File-Name", report.ServerSave.FileName);
        AddRequestHeaders(request, report.RequestId + "-server-save");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var details = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return new BugReportUploadResult(true, true, details);

        return new BugReportUploadResult(
            false,
            true,
            string.IsNullOrWhiteSpace(details)
                ? "The server-save endpoint returned " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + "."
                : details);
    }

    private void AddRequestHeaders(HttpRequestMessage request, string idempotencyKey)
    {
        request.Headers.TryAddWithoutValidation("apikey", supabasePublishableKey);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + authorizationToken);
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
    public int SchemaVersion => 2;
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

/// <summary>Contains one gzip-compressed log encoded as JSON base64.</summary>
internal sealed class BugReportJsonLog
{
    public string FileName { get; }
    public string ContentEncoding => "gzip+base64";
    public int UncompressedLength { get; }
    public byte[] Data { get; }

    public BugReportJsonLog(string fileName, int uncompressedLength, byte[] data)
    {
        FileName = fileName;
        UncompressedLength = uncompressedLength;
        Data = data;
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

/// <summary>Contains one pseudonymous client log in the JSON payload.</summary>
internal sealed class BugReportJsonClientLog
{
    public int ClientNumber { get; }
    public string FileName => "client-" + ClientNumber.ToString("D2", CultureInfo.InvariantCulture) + ".log";
    public string ContentEncoding => "gzip+base64";
    public int UncompressedLength { get; }
    public byte[] Data { get; }

    public BugReportJsonClientLog(int clientNumber, int uncompressedLength, byte[] data)
    {
        ClientNumber = clientNumber;
        UncompressedLength = uncompressedLength;
        Data = data;
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
