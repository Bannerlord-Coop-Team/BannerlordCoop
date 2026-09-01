using Coop.Core.Server.Services.Telemetry;
using System;
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
            new[] { "sessionId", "modVersion", "commit", "startedAt", "playerCount" },
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("41fa0000000000000000000000000000", root.GetProperty("sessionId").GetString());
        Assert.Equal("0.1.4", root.GetProperty("modVersion").GetString());
        Assert.Equal("abc123", root.GetProperty("commit").GetString());
        Assert.Equal("2026-08-29T22:15:00Z", root.GetProperty("startedAt").GetString());
        Assert.Equal(5, root.GetProperty("playerCount").GetInt32());
    }

    [Fact]
    public async Task RecordBattleStartedAsync_PostsToBattlesFoughtEdgeFunction()
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
    public async Task DefaultConfigurationUsesPublicEdgeFunction()
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
            "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/upsert-platform-statistics",
            ServerTelemetryUploader.Endpoint);
        Assert.Equal(ServerTelemetryUploader.Endpoint, handler.RequestUri?.ToString());
        Assert.Equal(
            "sb_publishable_tseZMeJ-RYeSHI0KwU2p0g_PE4GVtN6",
            ServerTelemetryUploader.PublishableKey);
        Assert.Equal(
            "https://wfvqnijwuyqjibhlcrhz.supabase.co/functions/v1/battles-fought-upsert",
            ServerTelemetryUploader.BattlesFoughtEndpoint);
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ContentType { get; private set; }
        public string? Authorization { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("accepted"),
            };
        }
    }
}
