using Common.LiveTesting;
using System.Diagnostics;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    // Identities and sequences from the captured native run b00937993e5e48a6b5c7eb6234b6bce8 (client1 control-status reads).
    private const string ControlIncarnation = "809ffd44-2622-408a-920a-986e227b7f4c";
    private const string PulseOperation = "6d58832a-f320-4d32-aed7-29a7b4679622";
    private const string NeutralOperation = "e65f0312-c6a5-4d46-9b80-98bae39c72c2";
    private const string NoOperation = "00000000-0000-0000-0000-000000000000";

    // Mirrors NavalLabCoordinator.ControlStatus wrapping NavalLabBehavior.InspectControlStatus.
    private static object Control(string operation, string phase, long first = 0, long last = 0, long neutral = 0, double rudder = 0.7, int longitudinal = 1,
        int slot = 0, string incarnation = ControlIncarnation, string statusIncarnation = ControlIncarnation, int hostEpoch = 1, bool terminal = false,
        bool blocked = false, string inputBlocker = null, double remaining = 0)
    {
        var application = new { rudder, lateral = rudder == 0 ? 0 : -1, longitudinal, doubleTap = longitudinal == 1 ? 1 : 0, sail = 0 };
        return new
        {
            incarnation, mode = "TwoClientNative", host = new { HostControllerId = "testclient1", Epoch = hostEpoch },
            status = new
            {
                IncarnationId = statusIncarnation, epoch = 1, owner = "testclient" + (slot + 1), ship = slot, ready = true, terminal, blocked, inputBlocker,
                operationId = operation, phase, pulseSynthetic = true, pulseFirstInputSequence = first, pulseLastInputSequence = last, pulseNeutralInputSequence = neutral,
                requestedLateralAxis = 0.5, requestedForwardAxis = 1.0, remainingSeconds = remaining,
                currentOwnerApplication = slot == 0 ? new object[] { application, null } : new object[] { null, application }, unavailable = (string)null,
            },
            unavailable = (string)null,
        };
    }

    private static LiveTestResponse ControlReply(object control, bool succeeded = true)
    {
        string json = JsonSerializer.Serialize(control);
        var process = new LiveTestProcessInfo { Pid = 2, Role = "client", PlatformId = "testclient1", RunToken = "run" };
        var response = LiveTestResponse.Success(Guid.NewGuid().ToString("N"), process, new
        {
            name = "coop.debug.naval_lab.control-status", arguments = Array.Empty<string>(), found = true, output = "LIVE_TEST_JSON=" + json,
            succeeded, errorCode = succeeded ? null : "control_rejected", hasStructuredResult = true, structuredResult = JsonSerializer.Deserialize<JsonElement>(json),
        });
        Assert.True(LiveTestProtocol.TryDeserializeResponse(LiveTestProtocol.SerializeResponse(response), out var real, out _));
        return real;
    }

    private static object CompletedPulse() => Control(PulseOperation, "completed_axes_neutral_requested", 1871, 1900, 1901);

    private async Task<(RunView Run, JsonElement Result)> WaitForControl(Func<int, LiveTestResponse> reply, string operation = PulseOperation,
        bool requireNeutral = false, int timeoutSeconds = 5, int readMilliseconds = 0)
    {
        var run = await runs.StartAsync("test", 1, default);
        int polls = 0;
        pipe.Reply = async (method, parameters, mutation, token) =>
        {
            Assert.Equal("command", method);
            Assert.False(mutation);
            var args = JsonSerializer.SerializeToElement(parameters);
            Assert.Equal("coop.debug.naval_lab.control-status", args.GetProperty("name").GetString());
            Assert.Empty(args.GetProperty("arguments").EnumerateArray());
            if (readMilliseconds > 0) await Task.Delay(readMilliseconds, token);
            return reply(Interlocked.Increment(ref polls));
        };
        var result = await runs.WaitForControlAsync(run.RunId, "client1", operation, ControlIncarnation, 0, requireNeutral, timeoutSeconds, default);
        return (run, JsonSerializer.SerializeToElement(result));
    }

    private static string[] ControlArtifacts(RunView run) => Directory.GetFiles(run.ArtifactDirectory, "client1-*.json");

    [Fact]
    public async Task ControlWaitObservesOneSecondPulseAtNativeReadRateAndReturnsOnlyTheFinalRecord()
    {
        var clock = Stopwatch.StartNew();
        var (run, result) = await WaitForControl(_ => ControlReply(clock.ElapsedMilliseconds < 1000
            ? Control(PulseOperation, "pending_synthetic_axes", 1871, 1872 + (clock.ElapsedMilliseconds / 16), remaining: 1 - (clock.ElapsedMilliseconds / 1000.0))
            : CompletedPulse()), readMilliseconds: 31);
        Assert.True(clock.ElapsedMilliseconds >= 1000);
        Assert.Equal("completed", result.GetProperty("outcome").GetString());
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.Equal("completed_axes_neutral_requested", result.GetProperty("phase").GetString());
        Assert.InRange(result.GetProperty("polls").GetInt32(), 3, 8);
        var status = result.GetProperty("response").GetProperty("Result").GetProperty("structuredResult").GetProperty("status");
        Assert.Equal(PulseOperation, status.GetProperty("operationId").GetString());
        Assert.Equal(1901, status.GetProperty("pulseNeutralInputSequence").GetInt64());
        string artifact = Assert.Single(ControlArtifacts(run));
        Assert.EndsWith(result.GetProperty("responseId").GetString() + ".json", artifact);
    }

    [Theory]
    [InlineData(PulseOperation)]
    [InlineData(NeutralOperation)]
    public async Task CapturedNativePulseReadsStayPendingAndNeverFalsePass(string operation)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "NativeControlStatus-b0099378.json")));
        var records = data.RootElement.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("structuredResult").Clone()).ToArray();
        Assert.Equal(18, records.Length);
        var (_, result) = await WaitForControl(poll => ControlReply(records[Math.Min(poll, records.Length) - 1]), operation, timeoutSeconds: 6, readMilliseconds: 31);
        Assert.True(result.GetProperty("polls").GetInt32() >= records.Length);
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        var last = result.GetProperty("last");
        Assert.Equal("pending_synthetic_axes", last.GetProperty("phase").GetString());
        Assert.Equal(0.48290260000067065, last.GetProperty("remainingSeconds").GetDouble());
        Assert.Equal(0.7, last.GetProperty("application").GetProperty("rudder").GetDouble());
    }

    [Fact]
    public async Task ControlWaitKeepsPriorCompletedOperationPendingUntilTheRequestedOperationCompletes()
    {
        var (_, result) = await WaitForControl(poll => ControlReply(poll switch
        {
            1 => CompletedPulse(),
            2 => Control(NeutralOperation, "pending_synthetic_axes", 1902, 1903, 0, 0, 0),
            _ => Control(NeutralOperation, "completed_axes_neutral_requested", 1902, 1930, 1931, 0, 0),
        }), NeutralOperation, requireNeutral: true);
        Assert.Equal("completed", result.GetProperty("outcome").GetString());
        Assert.Equal(3, result.GetProperty("polls").GetInt32());
        Assert.Equal(NeutralOperation, result.GetProperty("response").GetProperty("Result").GetProperty("structuredResult")
            .GetProperty("status").GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData(NoOperation, "not_requested")]
    [InlineData(PulseOperation, "completed_axes_neutral_requested")]
    [InlineData(NeutralOperation, "pending_synthetic_axes")]
    public async Task ControlWaitDeadlineReturnsSummaryOnlyForPermanentlyPendingOrOtherOperation(string operation, string phase)
    {
        var (run, result) = await WaitForControl(_ => ControlReply(Control(operation, phase, operation == NoOperation ? 0 : 1871, operation == NoOperation ? 0 : 1900,
            phase == "completed_axes_neutral_requested" ? 1901 : 0)), NeutralOperation, requireNeutral: true, timeoutSeconds: 1);
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        Assert.False(result.GetProperty("reached").GetBoolean());
        Assert.True(result.GetProperty("polls").GetInt32() > 1);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("response").ValueKind);
        var last = result.GetProperty("last");
        Assert.Equal(operation, last.GetProperty("operationId").GetString());
        Assert.Equal(new[] { "application", "operationId", "phase", "pulseFirstInputSequence", "pulseLastInputSequence", "pulseNeutralInputSequence", "remainingSeconds" },
            last.EnumerateObject().Select(p => p.Name).Order());
        Assert.Single(ControlArtifacts(run));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public async Task OnlyNeutralControlWaitRequiresZeroOwnerApplication(bool requireNeutral, int polls)
    {
        var applied = Control(NeutralOperation, "completed_axes_neutral_requested", 1902, 1930, 1931, rudder: 0.7, longitudinal: 1);
        var (_, result) = await WaitForControl(poll => ControlReply(poll < 3 ? applied
            : Control(NeutralOperation, "completed_axes_neutral_requested", 1902, 1930, 1931, rudder: 0, longitudinal: 0)), NeutralOperation, requireNeutral);
        Assert.Equal("completed", result.GetProperty("outcome").GetString());
        Assert.Equal(polls, result.GetProperty("polls").GetInt32());
    }

    [Fact]
    public async Task ControlWaitLateCompletionAfterDeadlineIsNotReported()
    {
        var clock = Stopwatch.StartNew();
        var (_, result) = await WaitForControl(_ => ControlReply(clock.ElapsedMilliseconds < 1500
            ? Control(PulseOperation, "pending_synthetic_axes", 1871, 1880) : CompletedPulse()), timeoutSeconds: 1, readMilliseconds: 31);
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        Assert.Equal("pending_synthetic_axes", result.GetProperty("last").GetProperty("phase").GetString());
    }

    [Fact]
    public async Task ControlWaitInFlightReadCancelledByDeadlineIsTheDeadlineNotAFault()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Reply = async (method, parameters, mutation, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { }
            return LiveTestResponse.Failure(Guid.NewGuid().ToString("N"), new LiveTestProcessInfo { Pid = 2 }, new LiveTestError("transport_deadline_expired", "fake", false));
        };
        var result = JsonSerializer.SerializeToElement(await runs.WaitForControlAsync(run.RunId, "client1", PulseOperation, ControlIncarnation, 0, false, 1, default));
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
        Assert.Empty(ControlArtifacts(run));
    }

    [Theory]
    [InlineData("incarnation")]
    [InlineData("status")]
    [InlineData("slot")]
    public async Task ControlWaitNeverAcceptsAnotherFixtureOrSlot(string defect)
    {
        const string other = "72a57da1-88b5-4f7e-9111-71f5c7a965f4";
        var (_, result) = await WaitForControl(_ => ControlReply(Control(PulseOperation, "completed_axes_neutral_requested", 1871, 1900, 1901,
            slot: defect == "slot" ? 1 : 0, incarnation: defect == "incarnation" ? other : ControlIncarnation,
            statusIncarnation: defect == "status" ? other : ControlIncarnation)));
        Assert.Equal("identity_mismatch", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
    }

    [Theory]
    [InlineData("terminal")]
    [InlineData("blocked")]
    [InlineData("input")]
    [InlineData("epoch")]
    public async Task ControlWaitStopsImmediatelyWhenTheOwnerBecomesIneligible(string defect)
    {
        var (_, result) = await WaitForControl(_ => ControlReply(Control(PulseOperation, "pending_synthetic_axes", 1871, 1880,
            terminal: defect == "terminal", blocked: defect == "blocked", inputBlocker: defect == "input" ? "window_not_focused" : null, hostEpoch: defect == "epoch" ? 2 : 1)));
        Assert.Equal("ineligible", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
    }

    [Theory]
    [InlineData("permission_lost_safety_stop", "control_failed")]
    [InlineData("failed_dispatch_safety_hold", "control_failed")]
    [InlineData("completed_without_neutral_sequence", "malformed")]
    public async Task ControlWaitReturnsNativeFailurePhasesAndInconsistentCompletionImmediately(string phase, string outcome)
    {
        var (_, result) = await WaitForControl(_ => ControlReply(phase == "completed_without_neutral_sequence"
            ? Control(PulseOperation, "completed_axes_neutral_requested", 1871, 1900, 0)
            : Control(PulseOperation, phase, 1871, 1880)));
        Assert.Equal(outcome, result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
        if (outcome == "control_failed") Assert.Equal(phase, result.GetProperty("phase").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControlWaitReturnsBridgeOrCommandFaultOnceWithoutRetry(bool bridgeError)
    {
        var (run, result) = await WaitForControl(_ => bridgeError
            ? LiveTestResponse.Failure(Guid.NewGuid().ToString("N"), new LiveTestProcessInfo { Pid = 2 }, new LiveTestError("game_thread_timeout", "fake", false))
            : ControlReply(CompletedPulse(), succeeded: false));
        Assert.Equal("fault", result.GetProperty("outcome").GetString());
        Assert.Equal("command", Assert.Single(pipe.Methods));
        if (bridgeError) Assert.Equal("game_thread_timeout", result.GetProperty("lastError").GetProperty("Code").GetString());
        Assert.Single(ControlArtifacts(run));
    }

    [Fact]
    public async Task ControlWaitCallerCancellationPropagatesAndReleasesGate()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Reply = (method, parameters, mutation, token) => Task.FromResult(method == "command"
            ? ControlReply(Control(PulseOperation, "pending_synthetic_axes", 1871, 1880))
            : LiveTestResponse.Success("other", new LiveTestProcessInfo { Pid = 2 }, new { }));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
        var wait = runs.WaitForControlAsync(run.RunId, "client1", PulseOperation, ControlIncarnation, 0, false, 10, cancelled.Token);
        await Task.Delay(100);
        // The gate is free during the 250 ms delay, so another tool completes inside the wait.
        Assert.True((await runs.RequestAsync(run.RunId, "client1", "screenshot-status", new { captureId = "c" }, false, default)).Ok);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.Single(ControlArtifacts(run), path => path.EndsWith("client1-other.json"));
    }

    [Theory]
    [InlineData("server", PulseOperation, ControlIncarnation, 0, 5)]
    [InlineData("client1", "not-a-uuid", ControlIncarnation, 0, 5)]
    [InlineData("client1", PulseOperation, NoOperation, 0, 5)]
    [InlineData("client1", PulseOperation, null, 0, 5)]
    [InlineData("client1", PulseOperation, ControlIncarnation, 2, 5)]
    [InlineData("client1", PulseOperation, ControlIncarnation, -1, 5)]
    [InlineData("client1", PulseOperation, ControlIncarnation, 0, 31)]
    [InlineData("client1", PulseOperation, ControlIncarnation, 0, 0)]
    public async Task ControlWaitRejectsInvalidRequestsBeforeAnyPoll(string instance, string operation, string incarnation, int slot, int timeoutSeconds)
    {
        var run = await runs.StartAsync("test", 1, default);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => runs.WaitForControlAsync(run.RunId, instance, operation, incarnation, slot, false, timeoutSeconds, default));
        Assert.Empty(pipe.Methods);
    }
}
