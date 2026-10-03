using Common.LiveTesting;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    private const string DriftOperation = "ea451533-21af-47ce-b889-887ee8da5e71";
    private const string DriftIncarnation = "72a57da1-88b5-4f7e-9111-71f5c7a965f4";

    // Mirrors LiveTestControlServer.HandleCommand and InspectDrift, round-tripped through the real protocol envelope.
    private static LiveTestResponse DriftReply(object drift, bool succeeded = true, bool structured = true)
    {
        string json = JsonSerializer.Serialize(drift);
        var process = new LiveTestProcessInfo { Pid = 2, Role = "client", PlatformId = "testclient1", RunToken = "run" };
        var response = LiveTestResponse.Success(Guid.NewGuid().ToString("N"), process, new
        {
            name = "coop.debug.naval_lab.drift-status", arguments = Array.Empty<string>(), found = true, output = "LIVE_TEST_JSON=" + json,
            succeeded, errorCode = succeeded ? null : "drift_rejected", hasStructuredResult = structured,
            structuredResult = structured ? JsonSerializer.Deserialize<JsonElement>(json) : (object)null,
        });
        Assert.True(LiveTestProtocol.TryDeserializeResponse(LiveTestProtocol.SerializeResponse(response), out var real, out _));
        return real;
    }

    private static object Drift(string ended, string operation = DriftOperation, string incarnation = DriftIncarnation) => new
    {
        incarnation, operationId = operation, requestedSeconds = 30, sampledSpanSeconds = ended == null ? 12.5 : 30.0, sampleCount = 601,
        ended, completeCoverage = ended == "duration_complete", rows = new[] { new { combatantId = "4c9d172c-6056-46fe-84f7-4a3c67679538", invalid = 0 } },
    };

    private async Task<(RunView Run, JsonElement Result)> WaitForDrift(Func<int, LiveTestResponse> reply, int timeoutSeconds = 10)
    {
        var run = await runs.StartAsync("test", 1, default);
        int polls = 0;
        pipe.Reply = (method, parameters, mutation, token) =>
        {
            Assert.Equal("command", method);
            Assert.False(mutation);
            var args = JsonSerializer.SerializeToElement(parameters);
            Assert.Equal("coop.debug.naval_lab.drift-status", args.GetProperty("name").GetString());
            Assert.Empty(args.GetProperty("arguments").EnumerateArray());
            return Task.FromResult(reply(++polls));
        };
        var result = await runs.WaitForDriftAsync(run.RunId, "client1", DriftOperation, DriftIncarnation, timeoutSeconds, default);
        return (run, JsonSerializer.SerializeToElement(result));
    }

    private static string[] DriftArtifacts(RunView run) => Directory.GetFiles(run.ArtifactDirectory, "client1-*.json");

    [Fact]
    public async Task DriftWaitReturnsOnlyTheEndedRecordingOnceAfterPendingPolls()
    {
        var (run, result) = await WaitForDrift(poll => DriftReply(Drift(poll < 3 ? null : "duration_complete")));
        Assert.Equal("ended", result.GetProperty("outcome").GetString());
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.Equal("duration_complete", result.GetProperty("ended").GetString());
        Assert.Equal(3, result.GetProperty("polls").GetInt32());
        var drift = result.GetProperty("response").GetProperty("Result").GetProperty("structuredResult");
        Assert.Equal(30.0, drift.GetProperty("sampledSpanSeconds").GetDouble());
        Assert.Equal(DriftOperation, drift.GetProperty("operationId").GetString());
        string artifact = Assert.Single(DriftArtifacts(run));
        Assert.EndsWith(result.GetProperty("responseId").GetString() + ".json", artifact);
    }

    [Fact]
    public async Task DriftWaitReturnsOtherEndedReasonsVerbatimForTheCallerToJudge()
    {
        var (_, result) = await WaitForDrift(_ => DriftReply(Drift("lifetime_or_terminal")));
        Assert.Equal("ended", result.GetProperty("outcome").GetString());
        Assert.Equal("lifetime_or_terminal", result.GetProperty("ended").GetString());
    }

    [Theory]
    [InlineData("b59396fc-13c6-42ab-98ac-958333797712", DriftIncarnation)]
    [InlineData(DriftOperation, "e65f0312-c6a5-4d46-9b80-98bae39c72c2")]
    public async Task DriftWaitNeverAcceptsAnotherRecordingOrFixture(string operation, string incarnation)
    {
        var (run, result) = await WaitForDrift(_ => DriftReply(Drift("duration_complete", operation, incarnation)));
        Assert.Equal("identity_mismatch", result.GetProperty("outcome").GetString());
        Assert.False(result.GetProperty("reached").GetBoolean());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
        Assert.Single(DriftArtifacts(run));
    }

    [Fact]
    public async Task DriftWaitStopsOnUnavailableRecording()
    {
        var (_, result) = await WaitForDrift(_ => DriftReply(new { unavailable = "not_started" }));
        Assert.Equal("unavailable", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
    }

    [Theory]
    [InlineData("structured")]
    [InlineData("ended")]
    [InlineData("identity")]
    public async Task DriftWaitRejectsMalformedPayloadsWithoutPolling(string defect)
    {
        var (_, result) = await WaitForDrift(_ => defect switch
        {
            "structured" => DriftReply(Drift(null), structured: false),
            "ended" => DriftReply(new { incarnation = DriftIncarnation, operationId = DriftOperation, ended = 1 }),
            _ => DriftReply(new { incarnation = DriftIncarnation, ended = (string)null }),
        });
        Assert.Equal("malformed", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DriftWaitReturnsBridgeOrCommandFaultOnceWithoutRetry(bool bridgeError)
    {
        var (run, result) = await WaitForDrift(_ => bridgeError
            ? LiveTestResponse.Failure(Guid.NewGuid().ToString("N"), new LiveTestProcessInfo { Pid = 2 }, new LiveTestError("game_thread_timeout", "fake", false))
            : DriftReply(Drift(null), succeeded: false));
        Assert.Equal("fault", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
        Assert.Equal("command", Assert.Single(pipe.Methods));
        if (bridgeError) Assert.Equal("game_thread_timeout", result.GetProperty("lastError").GetProperty("Code").GetString());
        else Assert.Equal("drift_rejected", result.GetProperty("response").GetProperty("Result").GetProperty("errorCode").GetString());
        Assert.Single(DriftArtifacts(run));
    }

    [Fact]
    public async Task DriftWaitReportsProcessExitWithoutSending()
    {
        var run = await runs.StartAsync("test", 1, default);
        await launcher.Processes[1].StopAsync(TimeSpan.Zero);
        var result = JsonSerializer.SerializeToElement(await runs.WaitForDriftAsync(run.RunId, "client1", DriftOperation, DriftIncarnation, 5, default));
        Assert.Equal("process_exited", result.GetProperty("outcome").GetString());
        Assert.Empty(pipe.Methods);
    }

    [Fact]
    public async Task DriftWaitDeadlineReturnsSummaryOnlyAndReleasesGateBetweenPolls()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Reply = (method, parameters, mutation, token) =>
            Task.FromResult(method == "command" ? DriftReply(Drift(null)) : LiveTestResponse.Success("other", new LiveTestProcessInfo { Pid = 2 }, new { }));
        var wait = runs.WaitForDriftAsync(run.RunId, "client1", DriftOperation, DriftIncarnation, 2, default);
        await Task.Delay(250);
        // The instance gate is free during the 500 ms delay, so another tool completes inside the wait.
        Assert.True((await runs.RequestAsync(run.RunId, "client1", "screenshot-status", new { captureId = "c" }, false, default)).Ok);
        Assert.False(wait.IsCompleted);
        var result = JsonSerializer.SerializeToElement(await wait);
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        Assert.True(result.GetProperty("deadlineExpired").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("response").ValueKind);
        var last = result.GetProperty("last");
        Assert.Equal(new[] { "incarnation", "operationId", "sampleCount", "sampledSpanSeconds" }, last.EnumerateObject().Select(p => p.Name).Order());
        Assert.Single(DriftArtifacts(run), path => !path.EndsWith("client1-other.json"));
    }

    [Fact]
    public async Task DriftWaitDeadlineIncludesTimeBlockedOnTheInstanceGate()
    {
        var run = await runs.StartAsync("test", 1, default);
        using var blocker = new CancellationTokenSource();
        pipe.Reply = async (method, parameters, mutation, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        };
        var held = runs.RequestAsync(run.RunId, "client1", "screenshot-status", new { captureId = "c" }, false, blocker.Token);
        var result = JsonSerializer.SerializeToElement(await runs.WaitForDriftAsync(run.RunId, "client1", DriftOperation, DriftIncarnation, 1, default)
            .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        Assert.Equal(0, result.GetProperty("polls").GetInt32());
        blocker.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => held);
        Assert.Empty(DriftArtifacts(run));
    }

    [Fact]
    public async Task DriftWaitCallerCancellationPropagatesAndReleasesGate()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Reply = (method, parameters, mutation, token) => Task.FromResult(DriftReply(Drift(null)));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runs.WaitForDriftAsync(run.RunId, "client1", DriftOperation, DriftIncarnation, 10, cancelled.Token));
        Assert.Empty(DriftArtifacts(run));
        pipe.Reply = (method, parameters, mutation, token) => Task.FromResult(DriftReply(Drift("duration_complete")));
        var result = JsonSerializer.SerializeToElement(await runs.WaitForDriftAsync(run.RunId, "client1", DriftOperation, DriftIncarnation, 2, default));
        Assert.Equal("ended", result.GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData("server", DriftOperation, DriftIncarnation, 40)]
    [InlineData("client1", "not-a-uuid", DriftIncarnation, 40)]
    [InlineData("client1", DriftOperation, "00000000-0000-0000-0000-000000000000", 40)]
    [InlineData("client1", DriftOperation, null, 40)]
    [InlineData("client1", DriftOperation, DriftIncarnation, 91)]
    [InlineData("client1", DriftOperation, DriftIncarnation, 0)]
    public async Task DriftWaitRejectsInvalidRequestsBeforeAnyPoll(string instance, string operation, string incarnation, int timeoutSeconds)
    {
        var run = await runs.StartAsync("test", 1, default);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => runs.WaitForDriftAsync(run.RunId, instance, operation, incarnation, timeoutSeconds, default));
        Assert.Empty(pipe.Methods);
    }
}
