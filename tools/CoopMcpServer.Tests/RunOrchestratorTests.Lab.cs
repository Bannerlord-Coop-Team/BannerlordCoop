using Common.LiveTesting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    // Incarnation of the captured two-client-native run 1790cd80e6a040f9bd12facd71942384 (server inspect reads).
    private const string LabIncarnation = "4731ac0f-de0d-4db5-bf65-2afb61303195";
    private const int DeployableRecord = 5;
    private const int ControlsReadyRecord = 7;

    private static JsonNode[] LabRecords()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "NavalLabServerInspect-1790cd80.json")));
        return data.RootElement.GetProperty("records").EnumerateArray().Select(r => JsonNode.Parse(r.GetProperty("structuredResult").GetRawText())).ToArray();
    }

    private static LiveTestResponse LabReply(JsonNode inspect)
    {
        string json = inspect.ToJsonString();
        var process = new LiveTestProcessInfo { Pid = 1, Role = "server", PlatformId = "testserver", RunToken = "run" };
        var response = LiveTestResponse.Success(Guid.NewGuid().ToString("N"), process, new
        {
            name = "coop.debug.naval_lab.inspect", arguments = Array.Empty<string>(), found = true, output = "LIVE_TEST_JSON=" + json,
            succeeded = true, errorCode = (string)null, hasStructuredResult = true, structuredResult = JsonSerializer.Deserialize<JsonElement>(json),
        });
        Assert.True(LiveTestProtocol.TryDeserializeResponse(LiveTestProtocol.SerializeResponse(response), out var real, out _));
        return real;
    }

    private static JsonNode LabRecord(int record, Action<JsonNode> change = null)
    {
        var node = LabRecords()[record - 1];
        change?.Invoke(node);
        return node;
    }

    private async Task<(RunView Run, JsonElement Result)> WaitForLab(Func<int, LiveTestResponse> reply, string stage, int timeoutSeconds = 5)
    {
        var run = await runs.StartAsync("test", 2, default);
        int polls = 0;
        pipe.Reply = (method, parameters, mutation, token) =>
        {
            Assert.Equal("command", method);
            Assert.False(mutation);
            var args = JsonSerializer.SerializeToElement(parameters);
            Assert.Equal("coop.debug.naval_lab.inspect", args.GetProperty("name").GetString());
            Assert.Empty(args.GetProperty("arguments").EnumerateArray());
            return Task.FromResult(reply(Interlocked.Increment(ref polls)));
        };
        var result = await runs.WaitForLabAsync(run.RunId, LabIncarnation, stage, timeoutSeconds, default);
        return (run, JsonSerializer.SerializeToElement(result));
    }

    private static string[] ServerArtifacts(RunView run) => Directory.GetFiles(run.ArtifactDirectory, "server-*.json");

    [Theory]
    [InlineData("deployable", DeployableRecord)]
    [InlineData("controls_ready", ControlsReadyRecord)]
    public async Task CapturedServerInspectReachesEachStageAtItsFirstQualifyingRecord(string stage, int record)
    {
        var records = LabRecords();
        Assert.Equal(7, records.Length);
        var (run, result) = await WaitForLab(poll => LabReply(records[Math.Min(poll, records.Length) - 1]), stage);
        Assert.Equal("reached", result.GetProperty("outcome").GetString());
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.Equal(stage, result.GetProperty("stage").GetString());
        Assert.Equal(record, result.GetProperty("polls").GetInt32());
        var last = result.GetProperty("last");
        Assert.Equal(new[] { "testclient1", "testclient2" }, last.GetProperty("ready").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(stage == "controls_ready", last.GetProperty("nativeControls").GetProperty("ready").GetBoolean());
        var manifest = result.GetProperty("response").GetProperty("Result").GetProperty("structuredResult").GetProperty("manifest");
        Assert.Equal(LabIncarnation, manifest.GetProperty("IncarnationId").GetString());
        string artifact = Assert.Single(ServerArtifacts(run));
        Assert.EndsWith(result.GetProperty("responseId").GetString() + ".json", artifact);
        Assert.Empty(Directory.GetFiles(run.ArtifactDirectory, "client*.json"));
    }

    [Fact]
    public async Task ControlsReadyWaitStaysPendingAfterDeployableAndReturnsSummaryOnlyAtDeadline()
    {
        var (run, result) = await WaitForLab(_ => LabReply(LabRecord(DeployableRecord)), "controls_ready", timeoutSeconds: 1);
        Assert.Equal("deadline_expired", result.GetProperty("outcome").GetString());
        Assert.False(result.GetProperty("reached").GetBoolean());
        Assert.True(result.GetProperty("polls").GetInt32() > 1);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("response").ValueKind);
        var controls = result.GetProperty("last").GetProperty("nativeControls");
        Assert.False(controls.GetProperty("ready").GetBoolean());
        Assert.False(controls.GetProperty("stationsCommitted").GetBoolean());
        Assert.Single(ServerArtifacts(run));
    }

    [Theory]
    [InlineData("identity_mismatch")]
    [InlineData("unavailable")]
    [InlineData("fixture_failed")]
    [InlineData("ineligible")]
    [InlineData("malformed")]
    public async Task LabWaitStopsAtTheFirstReadThatCanNeverBecomeReady(string outcome)
    {
        var inspect = LabRecord(ControlsReadyRecord, node =>
        {
            if (outcome == "identity_mismatch") node["manifest"]["IncarnationId"] = "72a57da1-88b5-4f7e-9111-71f5c7a965f4";
            if (outcome == "unavailable") node["manifest"] = null;
            if (outcome == "fixture_failed") node["failure"] = "campaign_write_blocked";
            if (outcome == "ineligible") node["host"]["Epoch"] = 2;
            if (outcome == "malformed") node.AsObject().Remove("ready");
        });
        var (_, result) = await WaitForLab(_ => LabReply(inspect), "deployable");
        Assert.Equal(outcome, result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
    }

    [Theory]
    [InlineData("deployable", "reached")]
    [InlineData("controls_ready", "ineligible")]
    public async Task OnlyControlsReadyRequiresNativeControls(string stage, string outcome)
    {
        var (_, result) = await WaitForLab(_ => LabReply(LabRecord(DeployableRecord, node => node["nativeControls"] = null)), stage);
        Assert.Equal(outcome, result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("polls").GetInt32());
    }

    [Fact]
    public async Task LabWaitReturnsBridgeFaultOnceWithoutRetry()
    {
        var (run, result) = await WaitForLab(_ => LiveTestResponse.Failure(Guid.NewGuid().ToString("N"), new LiveTestProcessInfo { Pid = 1 },
            new LiveTestError("game_thread_timeout", "fake", false)), "deployable");
        Assert.Equal("fault", result.GetProperty("outcome").GetString());
        Assert.Equal("game_thread_timeout", result.GetProperty("lastError").GetProperty("Code").GetString());
        Assert.Equal("command", Assert.Single(pipe.Methods));
        Assert.Single(ServerArtifacts(run));
    }

    [Theory]
    [InlineData("not-a-uuid", "deployable", 5)]
    [InlineData(NoOperation, "deployable", 5)]
    [InlineData(null, "deployable", 5)]
    [InlineData(LabIncarnation, "deployed", 5)]
    [InlineData(LabIncarnation, null, 5)]
    [InlineData(LabIncarnation, "controls_ready", 0)]
    [InlineData(LabIncarnation, "controls_ready", 31)]
    public async Task LabWaitRejectsInvalidRequestsBeforeAnyPoll(string incarnation, string stage, int timeoutSeconds)
    {
        var run = await runs.StartAsync("test", 2, default);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => runs.WaitForLabAsync(run.RunId, incarnation, stage, timeoutSeconds, default));
        Assert.Empty(pipe.Methods);
    }
}
