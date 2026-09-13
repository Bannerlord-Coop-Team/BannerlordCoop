using Common.LiveTesting;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace CoopMcpServer.TestHost;

public sealed class ScreenshotRunFixture(string directory) : IRunOrchestrator
{
    public List<string> Methods { get; } = new();
    public string FailureMethod { get; set; }
    public string CancelResponseMethod { get; set; }
    public string FailureCode { get; set; } = "stale_snapshot";
    public bool FrozenRender { get; set; }
    public bool PendingCapture { get; set; }
    public bool WrongPath { get; set; }
    public bool WrongHash { get; set; }
    private int frame;
    private int observations;
    private string path;
    public string ScreenshotPath(string runId, string instance)
    {
        Directory.CreateDirectory(directory);
        return path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".bmp");
    }
    public async Task<LiveTestResponse> RequestAsync(string runId, string instance, string method, object parameters, bool mutation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Methods.Add(method);
        var process = new LiveTestProcessInfo { Pid = 1, Role = "client", PlatformId = "fixture", RunToken = "fixture" };
        if (method == CancelResponseMethod)
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException)
            {
                return LiveTestResponse.Failure("fixture", process, new LiveTestError("operation_cancelled", "Transport cancellation response", mutation));
            }
        }
        if (method == FailureMethod)
            return LiveTestResponse.Failure("fixture", process, new LiveTestError(FailureCode, "exact bridge diagnostic", method == "screenshot"));
        object result;
        switch (method)
        {
            case "render-status":
                result = new { engineFrame = FrozenRender ? 1 : ++frame, rendererFps = 60, topScreen = "FixtureScreen", activeState = "FixtureState" };
                break;
            case "screenshot":
                File.WriteAllBytes(path, Bmp());
                result = new { captureId = new string('a', 32), captureRequested = true };
                break;
            case "screenshot-status":
                result = new { captureId = new string('a', 32), complete = ++observations > 1 && !PendingCapture,
                    path = WrongPath ? path + ".outside" : path, stable = true, basicQualityPassed = true,
                    sha256 = WrongHash ? "changed" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                    captureRequestEngineFrame = 2, observationEngineFrame = 4 };
                break;
            default: throw new NotSupportedException();
        }
        return LiveTestResponse.Success("fixture", process, JsonSerializer.SerializeToElement(result));
    }
    public static byte[] Bmp()
    {
        byte[] bmp = new byte[70];
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        void I32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(offset, 4), value);
        I32(2, bmp.Length); I32(10, 54); I32(14, 40); I32(18, 2); I32(22, 2);
        bmp[26] = 1; bmp[28] = 24;
        bmp[54] = 255; bmp[58] = 255; bmp[64] = 255; bmp[65] = 255;
        return bmp;
    }
    public Task<RunView> StartAsync(string profile, int count, CancellationToken token, string saveName = null) => throw new NotSupportedException();
    public SavePage ListSaves(string profile, int offset) => new SaveCatalog(new FixtureSaveDirectory(directory)).List(offset);
    private sealed class FixtureSaveDirectory(string directory) : ISaveDirectoryProvider
    {
        public string GetDirectory() => directory;
        public string GetSessionDirectory() => directory;
    }
    public PreflightReport Preflight(string profile, int count) => throw new NotSupportedException();
    public Task<ClientLaunchView> StartClientAsync(string runId, int index, CancellationToken token) => throw new NotSupportedException();
    public Task<RunView> GetAsync(string runId, CancellationToken token) => throw new NotSupportedException();
    public Task<object> WaitAsync(string runId, string instance, string state, int seconds, CancellationToken token) => throw new NotSupportedException();
    public Task<LogChunk> ReadLogsAsync(string runId, string instance, string cursor, int bytes, CancellationToken token) => throw new NotSupportedException();
    public Task<RunView> StopAsync(string runId) => throw new NotSupportedException();
    public Task StopAllAsync() => Task.CompletedTask;
}
