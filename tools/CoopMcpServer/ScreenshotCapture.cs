using Common.LiveTesting;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace CoopMcpServer;

public interface IScreenshotCapture
{
    Task<CallToolResult> CaptureAsync(string runId, string instance, int timeoutSeconds, CancellationToken cancellationToken);
}

public sealed class ScreenshotCapture : IScreenshotCapture
{
    private readonly IRunOrchestrator runs;
    private readonly IScreenshotImageEncoder encoder;
    public ScreenshotCapture(IRunOrchestrator runs, IScreenshotImageEncoder encoder) { this.runs = runs; this.encoder = encoder; }

    public async Task<CallToolResult> CaptureAsync(string runId, string instance, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (timeoutSeconds < 1 || timeoutSeconds > 120) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        string path = runs.ScreenshotPath(runId, instance);
        string captureId = null;
        bool requested = false;
        LiveTestResponse last = null;
        JsonElement? previousRender = null;
        try
        {
            while (true)
            {
                last = await runs.RequestAsync(runId, instance, "render-status", new { }, false, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (!last.Ok) return Result(new { complete = false, captureId, path, response = last }, true);
                var render = (JsonElement)last.Result;
                if (previousRender.HasValue && RenderAdvanced(previousRender.Value, render)) break;
                previousRender = render.Clone();
                await Task.Delay(100, deadline.Token);
            }
            requested = true;
            last = await runs.RequestAsync(runId, instance, "screenshot", new { path }, true, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            if (!last.Ok) return Result(new { complete = false, captureId, path, response = last }, true);
            captureId = ((JsonElement)last.Result).GetProperty("captureId").GetString();
            if (captureId == null || captureId.Length != 32 || captureId.Any(c => !char.IsAsciiHexDigit(c)))
                throw new IOException("Bridge returned an invalid capture id.");
            while (true)
            {
                last = await runs.RequestAsync(runId, instance, "screenshot-status", new { captureId }, false, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (!last.Ok) return Result(new { complete = false, captureId, path, response = last }, true);
                var evidence = (JsonElement)last.Result;
                if (evidence.GetProperty("complete").GetBoolean())
                {
                    if (!string.Equals(Path.GetFullPath(evidence.GetProperty("path").GetString()), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
                        !evidence.GetProperty("stable").GetBoolean() || !evidence.GetProperty("basicQualityPassed").GetBoolean() ||
                        evidence.GetProperty("captureRequestEngineFrame").GetInt32() == evidence.GetProperty("observationEngineFrame").GetInt32())
                        throw new IOException("Bridge completion lacks matching path, stability, quality, or frame evidence.");
                    deadline.Token.ThrowIfCancellationRequested();
                    var image = encoder.Encode(path, evidence.GetProperty("sha256").GetString(), deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    var result = Result(new { complete = true, captureId, path = image.Path, image.Sha256, image.Width, image.Height,
                        mimeType = "image/png", bytes = image.Png.Length, evidence,
                        semanticVisualCorrectnessEvaluated = false }, false);
                    result.Content.Add(ImageContentBlock.FromBytes(image.Png, "image/png"));
                    return result;
                }
                await Task.Delay(250, deadline.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return Result(new { complete = false, captureId, path, response = last,
                error = new LiveTestError(cancellationToken.IsCancellationRequested ? "operation_cancelled" : "capture_deadline_expired",
                    "Capture was not confirmed complete. Inspect artifacts or screenshot_status if captureId is known; never blindly repeat the request.", requested) }, true);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is InvalidOperationException || e is KeyNotFoundException || e is ArgumentException)
        {
            return Result(new { complete = false, captureId, path, response = last,
                error = new LiveTestError("capture_evidence_failed", e.Message, requested) }, true);
        }
    }

    private bool RenderAdvanced(JsonElement previous, JsonElement current) =>
        previous.GetProperty("engineFrame").GetInt32() != current.GetProperty("engineFrame").GetInt32() &&
        current.GetProperty("rendererFps").GetDouble() > 0 &&
        current.GetProperty("topScreen").ValueKind == JsonValueKind.String &&
        current.GetProperty("topScreen").GetString() == previous.GetProperty("topScreen").GetString() &&
        current.GetProperty("activeState").GetString() == previous.GetProperty("activeState").GetString();

    private CallToolResult Result(object metadata, bool error)
    {
        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new CallToolResult { IsError = error, StructuredContent = JsonSerializer.Deserialize<JsonElement>(json),
            Content = new List<ContentBlock> { new TextContentBlock { Text = json } } };
    }
}
