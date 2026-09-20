using Common.LiveTesting;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed partial class RunOrchestratorTests
{
    private const string StartupLayer = "0123456789abcdef0123456789abcdef";

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

    private void ConfigureStartupPopup(string inquiry = null, string failure = null, CancellationTokenSource cancelOnClick = null)
    {
        pipe.Status = new { readyForCampaignTests = true, topScreen = "SandBox.View.Map.NavalMapScreen", uiCapability = "bounded-ui-layers-v1" };
        bool clicked = false;
        pipe.Reply = (method, parameters, mutation, token) =>
        {
            var args = JsonSerializer.SerializeToElement(parameters);
            object result;
            if (method == "ui-layers")
            {
                Assert.False(mutation);
                if (clicked && failure == "confirmation_failed")
                    return Task.FromResult(LiveTestResponse.Failure("test", null, new LiveTestError("unavailable", "fake confirmation failure", false)));
                bool active = !clicked || failure == "still_open";
                result = new { screen = "NavalMapScreen", truncated = false, layers = new[] { new {
                    layer = StartupLayer, name = "QueryManager", active, visible = true, selectable = active,
                    rootCount = active ? 1 : 0, inputMask = active ? 3 : 0,
                } } };
            }
            else if (method == "ui-inspect")
            {
                Assert.False(mutation);
                Assert.Equal(StartupLayer, args.GetProperty("layer").GetString());
                Assert.Equal(0, args.GetProperty("offset").GetInt32());
                object Widget(string id, string type, string text, int parent) => new {
                    id, type, text, parent, visible = true, enabled = true, hitTestable = true, redacted = false,
                    actions = type == "ButtonWidget" ? new[] { "click" } : Array.Empty<string>(),
                };
                var elements = new[] {
                    new { reference = "e0", widget = Widget("SingleQueryPopupParent", "Widget", null, -1) },
                    new { reference = "e1", widget = Widget("Title", "AutoHideRichTextWidget", inquiry == "localized" ? "Der Ruf der Ozeane" : inquiry == "unrelated" ? "Troubled Waters" : "Call of the Oceans", 0) },
                    new { reference = "e2", widget = Widget("Description", "TextWidget", "Often, when you were growing up, you wondered if your destiny might lie upon the sea.", 0) },
                    new { reference = "e3", widget = Widget(inquiry == "cancel_button" ? "SingleQueryCancelButton" : "SingleQueryOkButton", "ButtonWidget", "Continue", 0) },
                };
                result = new { screen = "NavalMapScreen", snapshot = "fresh-snapshot", layer = StartupLayer, truncated = false,
                    scopeComplete = inquiry != "incomplete", total = elements.Length, elements };
            }
            else
            {
                Assert.Equal("ui-action", method);
                Assert.True(mutation);
                Assert.False(clicked);
                Assert.Equal("fresh-snapshot", args.GetProperty("snapshot").GetString());
                Assert.Equal("e3", args.GetProperty("element").GetString());
                Assert.Equal("click", args.GetProperty("action").GetString());
                clicked = true;
                cancelOnClick?.Cancel();
                token.ThrowIfCancellationRequested();
                if (failure == "uncertain")
                    return Task.FromResult(LiveTestResponse.Failure("test", null, new LiveTestError("request_deadline", "fake uncertain action", true)));
                result = new { dispatched = failure != "no_dispatch" };
            }
            return Task.FromResult(LiveTestResponse.Success("test", null, JsonSerializer.SerializeToElement(result)));
        };
    }
}
