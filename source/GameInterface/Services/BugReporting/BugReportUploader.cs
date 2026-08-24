using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameInterface.Services.BugReporting;

/// <summary>Posts diagnostic bug reports as JSON when an endpoint is configured.</summary>
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
    public const string SupabasePublishableKey = "sb_publishable_tseZMeJ-RYeSHI0KwU2p0g_PE4GVtN6";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient httpClient;
    private readonly string endpoint;
    private readonly string supabasePublishableKey;
    private readonly bool ownsClient;

    public bool IsConfigured =>
        !new Uri(endpoint).Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(supabasePublishableKey);

    public BugReportUploader() : this(
        new HttpClient { Timeout = TimeSpan.FromMinutes(2) },
        Endpoint,
        SupabasePublishableKey,
        true)
    {
    }

    internal BugReportUploader(
        HttpClient httpClient,
        string endpoint = Endpoint,
        string supabasePublishableKey = SupabasePublishableKey,
        bool ownsClient = false)
    {
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty.", nameof(endpoint));
        this.httpClient = httpClient;
        this.endpoint = endpoint;
        this.supabasePublishableKey = supabasePublishableKey;
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

        var json = JsonSerializer.Serialize(CreateRequest(report), JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("apikey", supabasePublishableKey);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + supabasePublishableKey);
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
    public int SchemaVersion => 1;
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
