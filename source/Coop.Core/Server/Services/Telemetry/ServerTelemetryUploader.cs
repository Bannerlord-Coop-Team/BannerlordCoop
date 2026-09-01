using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Telemetry;

public interface IServerTelemetryUploader
{
    bool IsConfigured { get; }
    Task<ServerTelemetryUploadResult> UploadAsync(
        ServerTelemetryStatus status,
        CancellationToken cancellationToken);
}

public interface IBattlesFoughtUploader
{
    Task<ServerTelemetryUploadResult> RecordBattleStartedAsync(CancellationToken cancellationToken);
}

/// <summary>Posts authenticated server statistics to the configured edge functions.</summary>
public class ServerTelemetryUploader : IServerTelemetryUploader, IBattlesFoughtUploader, IDisposable
{
    public const string Endpoint =
        "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/upsert-platform-statistics";
    public const string BattlesFoughtEndpoint =
        "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/battles-fought-upsert";
    internal const string PublishableKey = "sb_publishable_tseZMeJ-RYeSHI0KwU2p0g_PE4GVtN6";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient httpClient;
    private readonly string endpoint;
    private readonly string battlesFoughtEndpoint;
    private readonly bool ownsClient;

    public bool IsConfigured => IsEndpointConfigured(endpoint);

    public ServerTelemetryUploader() : this(
        new HttpClient { Timeout = TimeSpan.FromSeconds(15) },
        Endpoint,
        true)
    {
    }

    internal ServerTelemetryUploader(
        HttpClient httpClient,
        string endpoint = Endpoint,
        bool ownsClient = false,
        string battlesFoughtEndpoint = BattlesFoughtEndpoint)
    {
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(battlesFoughtEndpoint))
            throw new ArgumentException(
                "Battles-fought endpoint cannot be empty.",
                nameof(battlesFoughtEndpoint));

        this.httpClient = httpClient;
        this.endpoint = endpoint;
        this.battlesFoughtEndpoint = battlesFoughtEndpoint;
        this.ownsClient = ownsClient;
    }

    public async Task<ServerTelemetryUploadResult> UploadAsync(
        ServerTelemetryStatus status,
        CancellationToken cancellationToken)
    {
        if (status == null) throw new ArgumentNullException(nameof(status));
        if (!IsConfigured)
        {
            return new ServerTelemetryUploadResult(
                false,
                false,
                "The server telemetry endpoint is not configured.");
        }

        var json = JsonSerializer.Serialize(status, JsonOptions);
        return await PostAsync(endpoint, json, "server telemetry", cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServerTelemetryUploadResult> RecordBattleStartedAsync(
        CancellationToken cancellationToken)
    {
        if (!IsEndpointConfigured(battlesFoughtEndpoint))
        {
            return new ServerTelemetryUploadResult(
                false,
                false,
                "The battles-fought endpoint is not configured.");
        }

        return await PostAsync(battlesFoughtEndpoint, "{}", "battles-fought", cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ServerTelemetryUploadResult> PostAsync(
        string requestEndpoint,
        string json,
        string endpointName,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestEndpoint)
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublishableKey);
        request.Headers.TryAddWithoutValidation("apikey", PublishableKey);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var details = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new ServerTelemetryUploadResult(
                false,
                true,
                string.IsNullOrWhiteSpace(details)
                    ? "The " + endpointName + " endpoint returned " +
                      ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + "."
                    : details);
        }

        return new ServerTelemetryUploadResult(true, true, details);
    }

    private static bool IsEndpointConfigured(string requestEndpoint) =>
        !new Uri(requestEndpoint).Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (ownsClient) httpClient.Dispose();
    }
}

/// <summary>Describes the outcome of one server statistics request.</summary>
public sealed class ServerTelemetryUploadResult
{
    public bool Uploaded { get; }
    public bool EndpointConfigured { get; }
    public string Details { get; }

    public ServerTelemetryUploadResult(bool uploaded, bool endpointConfigured, string details)
    {
        Uploaded = uploaded;
        EndpointConfigured = endpointConfigured;
        Details = details;
    }
}
