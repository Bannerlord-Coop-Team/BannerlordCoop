using Common.LiveTesting;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    private const string StartupLayer = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task StartupPopupStaleInspectionRediscoversLoadingAndUsesNewReferencesOnce()
    {
        var run = await runs.StartAsync("test", 1, default);
        int discoveries = 0;
        ConfigureStartupPopup(staleInspections: 1, loadingMask: () => ++discoveries == 2 ? 7 : 0);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(
            run.RunId, "client1", "readyForCampaignTests", 5, default));
        Assert.Equal("dismissed", result.GetProperty("instance").GetProperty("StartupPopup").GetProperty("Outcome").GetString());
        Assert.Equal(new[] { "status", "ui-layers", "ui-inspect", "ui-layers", "ui-layers", "ui-inspect", "ui-action", "ui-layers" }, pipe.Methods);
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default);
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-action"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(8, false)]
    public async Task StartupPopupPermanentStaleInspectionStopsAtExistingBudget(int callerSeconds, bool callerExpired)
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup(staleInspections: int.MaxValue);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(
            run.RunId, "client1", "readyForCampaignTests", callerSeconds, default).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.Equal(callerExpired, result.GetProperty("deadlineExpired").GetBoolean());
        var popup = result.GetProperty("instance").GetProperty("StartupPopup");
        Assert.Equal("inspection_failed", popup.GetProperty("Outcome").GetString());
        Assert.Equal("stale_reference", popup.GetProperty("Error").GetProperty("Code").GetString());
        Assert.Equal(JsonValueKind.Null, popup.GetProperty("Action").ValueKind);
        Assert.True(pipe.Methods.Count(m => m == "ui-inspect") > 1);
        Assert.DoesNotContain("ui-action", pipe.Methods);
    }

    [Theory]
    [InlineData("inspection_uncertain")]
    [InlineData("inspection_error")]
    public async Task StartupPopupOtherInspectionFailuresAreNotRediscovered(string failure)
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup(failure: failure);
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 5, default);
        Assert.Equal(new[] { "status", "ui-layers", "ui-inspect" }, pipe.Methods);
        Assert.Equal("inspection_failed", (await runs.GetAsync(run.RunId, default)).Instances[1].StartupPopup.Outcome);
    }

    [Fact]
    public async Task StartupPopupCancellationDuringStaleRediscoveryReleasesGateWithoutAction()
    {
        var run = await runs.StartAsync("test", 1, default);
        using var cancelled = new CancellationTokenSource();
        ConfigureStartupPopup(staleInspections: 1);
        var reply = pipe.Reply;
        int discoveries = 0;
        pipe.Reply = (method, parameters, mutation, token) =>
        {
            if (method == "ui-layers" && ++discoveries == 2)
            {
                cancelled.Cancel();
                token.ThrowIfCancellationRequested();
            }
            return reply(method, parameters, mutation, token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 5, cancelled.Token));
        var popup = (await runs.GetAsync(run.RunId, default)).Instances[1].StartupPopup;
        Assert.Equal("stale_reference", popup.Error.Code);
        Assert.Null(popup.Action);
        Assert.DoesNotContain("ui-action", pipe.Methods);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupPopupWaitsForLoadingBeyondGraceBeforeOneFreshDismissal(bool deferred)
    {
        var run = await runs.StartAsync("test", deferred ? 0 : 1, default);
        if (deferred) await runs.StartClientAsync(run.RunId, 1, default);
        int discoveries = 0;
        ConfigureStartupPopup(loadingMask: () => ++discoveries <= 7 ? 7 : 0);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(
            run.RunId, "client1", "readyForCampaignTests", 8, default).WaitAsync(TimeSpan.FromSeconds(12)));
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.False(result.GetProperty("deadlineExpired").GetBoolean());
        Assert.Equal("dismissed", result.GetProperty("instance").GetProperty("StartupPopup").GetProperty("Outcome").GetString());
        Assert.Equal(9, discoveries); // Seven obstructed polls, fresh eligibility and closure.
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-inspect"));
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-action"));
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default);
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-action"));
    }

    [Fact]
    public async Task StartupPopupPermanentLoadingExpiresAtCallerDeadlineWithoutAction()
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup(loadingMask: () => 7);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(
            run.RunId, "client1", "readyForCampaignTests", 1, default).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.True(result.GetProperty("deadlineExpired").GetBoolean());
        Assert.Equal("loading_obstructed", result.GetProperty("instance").GetProperty("StartupPopup").GetProperty("Outcome").GetString());
        Assert.DoesNotContain("ui-inspect", pipe.Methods);
        Assert.DoesNotContain("ui-action", pipe.Methods);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupPopupLoadingCancellationKeepsObstructionAndReleasesGate(bool pipeReturnsCancellation)
    {
        var run = await runs.StartAsync("test", 1, default);
        using var cancelled = new CancellationTokenSource();
        ConfigureStartupPopup(loadingMask: () => 7);
        var reply = pipe.Reply;
        int reads = 0;
        pipe.Reply = (method, parameters, mutation, token) =>
        {
            if (method == "ui-layers" && ++reads == 2)
            {
                cancelled.Cancel();
                if (pipeReturnsCancellation)
                    return Task.FromResult(LiveTestResponse.Failure("cancelled-read", null,
                        new LiveTestError("operation_cancelled", "cancelled loading read", false)));
                token.ThrowIfCancellationRequested();
            }
            return reply(method, parameters, mutation, token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 5, cancelled.Token).WaitAsync(TimeSpan.FromSeconds(8)));
        var popup = (await runs.GetAsync(run.RunId, default)).Instances[1].StartupPopup;
        Assert.Equal("loading_obstructed", popup.Outcome);
        Assert.Equal("startup_popup_loading_obstructed", popup.Error.Code);
        Assert.Null(popup.Action);
        Assert.DoesNotContain("ui-inspect", pipe.Methods);
        Assert.DoesNotContain("ui-action", pipe.Methods);
    }

    [Fact]
    public async Task StartupPopupClearedLoadingWithoutInquiryRemainsNotPresent()
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup(inquiry: "absent", loadingMask: () => 0);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(
            run.RunId, "client1", "readyForCampaignTests", 1, default));
        Assert.True(result.GetProperty("deadlineExpired").GetBoolean());
        Assert.Equal("not_present", result.GetProperty("instance").GetProperty("StartupPopup").GetProperty("Outcome").GetString());
        Assert.DoesNotContain("ui-inspect", pipe.Methods);
        Assert.DoesNotContain("ui-action", pipe.Methods);
    }

    [Fact]
    public async Task StartupPopupDismissalRequiresFreshClosureAndNeverClicksAgain()
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup();
        var first = JsonSerializer.SerializeToElement(await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 5, default));
        Assert.True(first.GetProperty("reached").GetBoolean());
        Assert.Equal("dismissed", first.GetProperty("instance").GetProperty("StartupPopup").GetProperty("Outcome").GetString());
        Assert.Equal(new[] { "status", "ui-layers", "ui-inspect", "ui-action", "ui-layers" }, pipe.Methods);
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default);
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-action"));
    }

    [Theory]
    [InlineData("uncertain")]
    [InlineData("action_stale")]
    [InlineData("still_open")]
    [InlineData("no_dispatch")]
    [InlineData("confirmation_failed")]
    public async Task StartupPopupUnconfirmedActionIsNeverReplayed(string failure)
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup(failure: failure);
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 5, default);
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default);
        Assert.Equal("unconfirmed", (await runs.GetAsync(run.RunId, default)).Instances[1].StartupPopup.Outcome);
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-action"));
    }

    [Theory]
    [InlineData("unrelated")]
    [InlineData("localized")]
    [InlineData("cancel_button")]
    [InlineData("incomplete")]
    [InlineData("upper_modal")]
    public async Task StartupPopupRejectsOtherInquiriesAndIncompleteScopesWithinDeadline(string inquiry)
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup(inquiry: inquiry);
        var result = JsonSerializer.SerializeToElement(await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default));
        Assert.True(result.GetProperty("reached").GetBoolean());
        Assert.True(result.GetProperty("deadlineExpired").GetBoolean());
        Assert.Equal("not_actionable", result.GetProperty("instance").GetProperty("StartupPopup").GetProperty("Outcome").GetString());
        Assert.DoesNotContain("ui-action", pipe.Methods);
    }

    [Fact]
    public async Task StartupPopupMissingCapabilityLeavesReadyClientUnsupportedWithoutUiRequests()
    {
        var run = await runs.StartAsync("test", 1, default);
        pipe.Status = new { readyForCampaignTests = true, topScreen = "SandBox.View.Map.NavalMapScreen" };
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default);
        Assert.Equal(new[] { "status" }, pipe.Methods);
        Assert.Equal("unsupported", (await runs.GetAsync(run.RunId, default)).Instances[1].StartupPopup.Outcome);
    }

    [Theory]
    [InlineData("server", "readyForCampaignTests")]
    [InlineData("client1", "controlReady")]
    public async Task StartupPopupDoesNotMutateOtherWaits(string instance, string state)
    {
        var run = await runs.StartAsync("test", 1, default);
        ConfigureStartupPopup();
        await runs.WaitAsync(run.RunId, instance, state, 1, default);
        Assert.Equal(new[] { "status" }, pipe.Methods);
    }

    [Fact]
    public async Task StartupPopupCallerCancellationAfterSendConsumesAttemptAndReleasesGate()
    {
        var run = await runs.StartAsync("test", 1, default);
        using var cancelled = new CancellationTokenSource();
        ConfigureStartupPopup(cancelOnClick: cancelled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 5, cancelled.Token));
        await runs.WaitAsync(run.RunId, "client1", "readyForCampaignTests", 1, default);
        Assert.Equal(1, pipe.Methods.Count(m => m == "ui-action"));
        Assert.Equal("unconfirmed", (await runs.GetAsync(run.RunId, default)).Instances[1].StartupPopup.Outcome);
    }

    private void ConfigureStartupPopup(string inquiry = null, string failure = null, CancellationTokenSource cancelOnClick = null,
        Func<int> loadingMask = null, int staleInspections = 0)
    {
        pipe.Status = new { readyForCampaignTests = true, topScreen = "SandBox.View.Map.NavalMapScreen", uiCapability = "bounded-ui-layers-v1" };
        bool clicked = false;
        int discoveries = 0;
        int inspections = 0;
        string currentLayer = StartupLayer;
        string snapshot = "fresh-snapshot";
        string buttonReference = "e3";
        pipe.Reply = (method, parameters, mutation, token) =>
        {
            var args = JsonSerializer.SerializeToElement(parameters);
            object result;
            if (method == "ui-layers")
            {
                Assert.False(mutation);
                if (clicked && failure == "confirmation_failed")
                    return Task.FromResult(LiveTestResponse.Failure("test", null, new LiveTestError("unavailable", "fake confirmation failure", false)));
                if (staleInspections > 0)
                {
                    currentLayer = (++discoveries).ToString("x32");
                    snapshot = "snapshot-" + currentLayer;
                    buttonReference = "continue-" + currentLayer;
                }
                bool active = inquiry != "absent" && (!clicked || failure == "still_open");
                var layers = new List<object> { new {
                    layer = currentLayer, name = "QueryManager", stackIndex = 13, active, visible = true, selectable = active,
                    rootCount = active ? 1 : 0, inputMask = active ? 3 : 0,
                } };
                if (loadingMask != null || inquiry == "upper_modal")
                    layers.Add(new { name = inquiry == "upper_modal" ? "OtherModal" : "LoadingWindow",
                        stackIndex = 18, active = true, inputMask = loadingMask?.Invoke() ?? 7 });
                result = new { screen = "NavalMapScreen", truncated = false, layers };
            }
            else if (method == "ui-inspect")
            {
                Assert.False(mutation);
                Assert.Equal(currentLayer, args.GetProperty("layer").GetString());
                Assert.Equal(0, args.GetProperty("offset").GetInt32());
                if (++inspections <= staleInspections || failure is "inspection_uncertain" or "inspection_error")
                    return Task.FromResult(LiveTestResponse.Failure("stale-inspection", null, new LiveTestError(
                        failure == "inspection_error" ? "unavailable" : "stale_reference",
                        "Layer stack or modal roots changed; discover layers again.", failure == "inspection_uncertain")));
                object Widget(string id, string type, string text, int parent) => new {
                    id, type, text, parent, visible = true, enabled = true, hitTestable = inquiry != "upper_modal", redacted = false,
                    actions = type == "ButtonWidget" ? new[] { "click" } : Array.Empty<string>(),
                };
                var elements = new[] {
                    new { reference = "e0", widget = Widget("SingleQueryPopupParent", "Widget", null, -1) },
                    new { reference = "e1", widget = Widget("Title", "AutoHideRichTextWidget", inquiry == "localized" ? "Der Ruf der Ozeane" : inquiry == "unrelated" ? "Troubled Waters" : "Call of the Oceans", 0) },
                    new { reference = "e2", widget = Widget("Description", "TextWidget", "Often, when you were growing up, you wondered if your destiny might lie upon the sea.", 0) },
                    new { reference = buttonReference, widget = Widget(inquiry == "cancel_button" ? "SingleQueryCancelButton" : "SingleQueryOkButton", "ButtonWidget", "Continue", 0) },
                };
                result = new { screen = "NavalMapScreen", snapshot, layer = currentLayer, truncated = false,
                    scopeComplete = inquiry != "incomplete", total = elements.Length, elements };
            }
            else
            {
                Assert.Equal("ui-action", method);
                Assert.True(mutation);
                Assert.False(clicked);
                Assert.Equal(snapshot, args.GetProperty("snapshot").GetString());
                Assert.Equal(buttonReference, args.GetProperty("element").GetString());
                Assert.Equal("click", args.GetProperty("action").GetString());
                clicked = true;
                cancelOnClick?.Cancel();
                token.ThrowIfCancellationRequested();
                if (failure is "uncertain" or "action_stale")
                    return Task.FromResult(LiveTestResponse.Failure("test", null, new LiveTestError(
                        failure == "action_stale" ? "stale_reference" : "request_deadline", "fake action failure", failure == "uncertain")));
                result = new { dispatched = failure != "no_dispatch" };
            }
            return Task.FromResult(LiveTestResponse.Success("test", null, JsonSerializer.SerializeToElement(result)));
        };
    }
}
