using Common.LiveTesting;
using System.Text.Json;

namespace CoopMcpServer;

public sealed record StartupPopupView(string Outcome, LiveTestResponse Action = null, LiveTestError Error = null);

public sealed partial class RunOrchestrator
{
    private async Task TryDismissStartupPopupAsync(Run run, Instance target, CancellationToken cancellationToken)
    {
        await target.Gate.WaitAsync(cancellationToken);
        try
        {
            if (target.StartupPopupActionAttempted) return;
            if (!Matches(target, "readyForCampaignTests") || target.Status is not JsonElement status ||
                status.GetProperty("topScreen").GetString()?.EndsWith(".NavalMapScreen", StringComparison.Ordinal) != true)
            {
                target.StartupPopup = new("not_applicable");
                return;
            }
            if (!status.TryGetProperty("uiCapability", out var capability) ||
                capability.GetString() != "bounded-ui-layers-v1")
            {
                target.StartupPopup = new("unsupported");
                return;
            }

            // Campaign readiness can precede the first rendered inquiry; never extend the caller's deadline.
            using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            grace.CancelAfter(TimeSpan.FromSeconds(3));
            target.StartupPopup = new("not_observed");
            try
            {
                do
                {
                    var layers = await ReadStartupPopupAsync(run, target, "ui-layers", new { }, grace.Token);
                    if (!TryQueryLayer(layers, out var query))
                    {
                        if (layers.ValueKind == JsonValueKind.Object) target.StartupPopup = new("not_actionable");
                        return;
                    }
                    if (!query.GetProperty("active").GetBoolean() && query.GetProperty("rootCount").GetInt32() == 0)
                        target.StartupPopup = new("not_present");
                    else if (query.GetProperty("active").GetBoolean() && query.GetProperty("visible").GetBoolean() &&
                        query.GetProperty("selectable").GetBoolean() && query.GetProperty("rootCount").GetInt32() == 1)
                    {
                        string layer = query.GetProperty("layer").GetString();
                        var inspection = await ReadStartupPopupAsync(run, target, "ui-inspect", new { layer, offset = 0 }, grace.Token);
                        if (inspection.ValueKind != JsonValueKind.Object) return;
                        string button = FindStartupContinue(inspection, layer);
                        if (button != null)
                        {
                            grace.Token.ThrowIfCancellationRequested();
                            // Consume before sending, including transport failures and uncertain native callbacks.
                            target.StartupPopupActionAttempted = true;
                            target.StartupPopup = new("unconfirmed");
                            var action = await pipe.SendAsync(target.Identity, "ui-action", new
                            {
                                snapshot = inspection.GetProperty("snapshot").GetString(), element = button, action = "click",
                            }, true, grace.Token);
                            action = RecordResponse(run, target, action, true);
                            target.StartupPopup = new("unconfirmed", action, action.Error);
                            var confirmation = await ReadStartupPopupAsync(run, target, "ui-layers", new { }, grace.Token);
                            bool dispatched = action.Ok && action.Result is JsonElement result &&
                                result.TryGetProperty("dispatched", out var flag) && flag.ValueKind == JsonValueKind.True;
                            if (dispatched && TryQueryLayer(confirmation, out var after) &&
                                !after.GetProperty("active").GetBoolean() && after.GetProperty("rootCount").GetInt32() == 0 &&
                                after.GetProperty("inputMask").GetInt32() == 0)
                                target.StartupPopup = new("dismissed", action);
                            return;
                        }
                        target.StartupPopup = new("not_actionable");
                    }
                    else target.StartupPopup = new("not_actionable");
                    await Task.Delay(500, grace.Token);
                } while (true);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        }
        catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException ||
            exception is KeyNotFoundException)
        {
            target.StartupPopup = new(target.StartupPopupActionAttempted ? "unconfirmed" : "inspection_failed",
                target.StartupPopup?.Action, new LiveTestError("startup_popup_inspection_failed", exception.Message, false));
        }
        finally { target.Gate.Release(); }
    }

    private async Task<JsonElement> ReadStartupPopupAsync(Run run, Instance target, string method, object parameters,
        CancellationToken cancellationToken)
    {
        var response = await pipe.SendAsync(target.Identity, method, parameters, false, cancellationToken);
        response = RecordResponse(run, target, response, false);
        if (!response.Ok || response.Result is not JsonElement result)
        {
            target.StartupPopup = new(target.StartupPopupActionAttempted ? "unconfirmed" : "inspection_failed",
                target.StartupPopup?.Action, response.Error);
            return default;
        }
        return result;
    }

    private bool TryQueryLayer(JsonElement result, out JsonElement query)
    {
        query = default;
        if (result.ValueKind != JsonValueKind.Object || result.GetProperty("truncated").GetBoolean() ||
            result.GetProperty("screen").GetString() != "NavalMapScreen") return false;
        var queries = result.GetProperty("layers").EnumerateArray()
            .Where(layer => layer.GetProperty("name").GetString() == "QueryManager").ToArray();
        if (queries.Length != 1) return false;
        query = queries[0];
        return true;
    }

    private string FindStartupContinue(JsonElement inspection, string layer)
    {
        if (inspection.ValueKind != JsonValueKind.Object || inspection.GetProperty("truncated").GetBoolean() ||
            !inspection.GetProperty("scopeComplete").GetBoolean() || inspection.GetProperty("layer").GetString() != layer ||
            inspection.GetProperty("screen").GetString() != "NavalMapScreen") return null;
        var elements = inspection.GetProperty("elements").EnumerateArray().ToArray();
        // Fail closed rather than paging a changed or unexpectedly large query domain.
        if (elements.Length != inspection.GetProperty("total").GetInt32()) return null;
        var widgets = elements.Select(element => element.GetProperty("widget")).ToArray();
        bool Is(JsonElement widget, string property, string value) => widget.GetProperty(property).GetString() == value;
        bool Visible(JsonElement widget) => widget.GetProperty("visible").GetBoolean();
        bool InPopup(int index, int popup)
        {
            for (int remaining = widgets.Length; remaining > 0 && index >= 0 && index < widgets.Length; remaining--)
            {
                if (index == popup) return true;
                index = widgets[index].GetProperty("parent").GetInt32();
            }
            return false;
        }
        var popups = Enumerable.Range(0, widgets.Length)
            .Where(i => Is(widgets[i], "id", "SingleQueryPopupParent") && Visible(widgets[i])).ToArray();
        if (popups.Length != 1) return null;
        var contents = Enumerable.Range(0, widgets.Length).Where(i => InPopup(i, popups[0]) && Visible(widgets[i])).ToArray();
        // NavalInitializationCampaignBehavior.OnCharacterCreationIsOver: SJT8Nl5a and XcaoQSjv (English only).
        if (!contents.Any(i => Is(widgets[i], "type", "AutoHideRichTextWidget") && Is(widgets[i], "text", "Call of the Oceans")) ||
            !contents.Any(i => Is(widgets[i], "id", "Description") &&
                widgets[i].GetProperty("text").GetString()?.StartsWith(
                    "Often, when you were growing up, you wondered if your destiny might lie upon the sea.", StringComparison.Ordinal) == true) ||
            contents.Any(i => Is(widgets[i], "id", "SingleQueryCancelButton"))) return null;
        var buttons = contents.Where(i => Is(widgets[i], "id", "SingleQueryOkButton") &&
            Is(widgets[i], "type", "ButtonWidget") && Is(widgets[i], "text", "Continue") &&
            widgets[i].GetProperty("enabled").GetBoolean() && widgets[i].GetProperty("hitTestable").GetBoolean() &&
            !widgets[i].GetProperty("redacted").GetBoolean() &&
            widgets[i].GetProperty("actions").EnumerateArray().Any(action => action.GetString() == "click")).ToArray();
        return buttons.Length == 1 ? elements[buttons[0]].GetProperty("reference").GetString() : null;
    }
}
