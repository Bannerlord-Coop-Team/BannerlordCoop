using Coop.Core.Server.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Server.Services.Telemetry;

public class ServerTelemetryUploaderTests
{
    [Fact]
    public async Task UploadAsync_PostsExpectedServerStatusJson()
    {
        var handler = new RecordingHttpHandler();
        using var httpClient = new HttpClient(handler);
        using var uploader = new ServerTelemetryUploader(
            httpClient,
            "https://telemetry.example.test/api/v1/status");
        var startedAt = new DateTime(2026, 8, 29, 22, 15, 0, DateTimeKind.Utc);
        var status = new ServerTelemetryStatus(
            "41fa0000000000000000000000000000",
            "0.1.4",
            "abc123",
            startedAt,
            5);

        var result = await uploader.UploadAsync(status, CancellationToken.None);

        Assert.True(result.Uploaded);
        Assert.True(result.EndpointConfigured);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://telemetry.example.test/api/v1/status", handler.RequestUri?.ToString());
        Assert.Equal("application/json; charset=utf-8", handler.ContentType);
        Assert.Equal("Bearer " + ServerTelemetryUploader.PublishableKey, handler.Authorization);
        Assert.Equal(ServerTelemetryUploader.PublishableKey, handler.ApiKey);

        using var json = JsonDocument.Parse(handler.Body ?? string.Empty);
        var root = json.RootElement;
        Assert.Equal(
            new[] { "p_session_id", "p_server_id", "p_mod_version", "p_commit_hash", "p_started_at", "p_player_count" },
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("41fa0000000000000000000000000000", root.GetProperty("p_session_id").GetString());
        Assert.Equal("41fa0000000000000000000000000000", root.GetProperty("p_server_id").GetString());
        Assert.Equal("0.1.4", root.GetProperty("p_mod_version").GetString());
        Assert.Equal("abc123", root.GetProperty("p_commit_hash").GetString());
        Assert.Equal("2026-08-29T22:15:00Z", root.GetProperty("p_started_at").GetString());
        Assert.Equal(5, root.GetProperty("p_player_count").GetInt32());
    }

    [Fact]
    public async Task RecordBattleStartedAsync_PostsToBattlesFoughtRpc()
    {
        const string testEndpoint = "https://telemetry.example.test/api/v1/battles-fought";
        var handler = new RecordingHttpHandler();
        using var httpClient = new HttpClient(handler);
        using var uploader = new ServerTelemetryUploader(
            httpClient,
            battlesFoughtEndpoint: testEndpoint);

        var result = await uploader.RecordBattleStartedAsync(CancellationToken.None);

        Assert.True(result.Uploaded);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(testEndpoint, handler.RequestUri?.ToString());
        Assert.Equal("application/json; charset=utf-8", handler.ContentType);
        Assert.Equal("Bearer " + ServerTelemetryUploader.PublishableKey, handler.Authorization);
        Assert.Equal(ServerTelemetryUploader.PublishableKey, handler.ApiKey);
        Assert.Equal("{}", handler.Body);
    }

    [Fact]
    public async Task UploadAsync_FallsBackToLegacyRpcOnNotFoundWithoutServerId()
    {
        var handler = new RecordingHttpHandler(HttpStatusCode.NotFound, HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        using var uploader = new ServerTelemetryUploader(httpClient);
        var status = new ServerTelemetryStatus(
            "stable-server",
            "session",
            "0.1.4",
            "abc123",
            new DateTime(2026, 8, 29, 22, 15, 0, DateTimeKind.Utc),
            5);

        var result = await uploader.UploadAsync(status, CancellationToken.None);

        Assert.True(result.Uploaded);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(ServerTelemetryUploader.Endpoint, handler.RequestUris[0]?.ToString());
        Assert.Equal(ServerTelemetryUploader.LegacyEndpoint, handler.RequestUris[1]?.ToString());
        using var json = JsonDocument.Parse(handler.Bodies[1]);
        Assert.False(json.RootElement.TryGetProperty("p_server_id", out _));
        Assert.Equal("session", json.RootElement.GetProperty("p_session_id").GetString());
    }

    [Fact]
    public async Task DefaultConfigurationUsesPublicRpcEndpoints()
    {
        var handler = new RecordingHttpHandler();
        using var httpClient = new HttpClient(handler);
        using var uploader = new ServerTelemetryUploader(httpClient);
        var status = new ServerTelemetryStatus(
            "session",
            "0.1.4",
            "abc123",
            DateTime.UtcNow,
            0);

        var result = await uploader.UploadAsync(status, CancellationToken.None);

        Assert.True(uploader.IsConfigured);
        Assert.True(result.Uploaded);
        Assert.True(result.EndpointConfigured);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(
            "https://wfvqnijwuyqjibhlcrhz.supabase.co/rest/v1/rpc/report_server_presence",
            ServerTelemetryUploader.Endpoint);
        Assert.Equal(ServerTelemetryUploader.Endpoint, handler.RequestUri?.ToString());
        Assert.Equal(
            "sb_publishable_tseZMeJ-RYeSHI0KwU2p0g_PE4GVtN6",
            ServerTelemetryUploader.PublishableKey);
        Assert.Equal(
            "https://wfvqnijwuyqjibhlcrhz.supabase.co/rest/v1/rpc/increment_battles_fought",
            ServerTelemetryUploader.BattlesFoughtEndpoint);
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> responseStatuses;

        public int RequestCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ContentType { get; private set; }
        public string? Authorization { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }
        public List<Uri?> RequestUris { get; } = new List<Uri?>();
        public List<string> Bodies { get; } = new List<string>();

        public RecordingHttpHandler(params HttpStatusCode[] responseStatuses)
        {
            this.responseStatuses = new Queue<HttpStatusCode>(responseStatuses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            ApiKey = request.Headers.TryGetValues("apikey", out var apiKeys)
                ? string.Join(string.Empty, apiKeys)
                : null;
            Body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync();
            RequestUris.Add(RequestUri);
            Bodies.Add(Body);

            var statusCode = responseStatuses.Count == 0
                ? HttpStatusCode.OK
                : responseStatuses.Dequeue();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("accepted"),
            };
        }
    }
}
