using Common.LiveTesting;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CoopMcpServer;

public interface IDebugTools
{
    Task<RunView> StartRun(string profile, int client_count, CancellationToken cancellationToken);
    Task<RunView> GetRun(string run_id, CancellationToken cancellationToken);
    Task<object> WaitForState(string run_id, string instance, string state, int timeout_seconds, CancellationToken cancellationToken);
    Task<LiveTestResponse> ListCommands(string run_id, string instance, CancellationToken cancellationToken);
    Task<LiveTestResponse> ExecuteCommand(string run_id, string instance, string name, string[] arguments, CancellationToken cancellationToken);
    Task<LiveTestResponse> JoinClient(string run_id, string instance, CancellationToken cancellationToken);
    Task<LogChunk> ReadLogs(string run_id, string instance, CancellationToken cancellationToken, string cursor = null, int max_bytes = 16384);
    Task<LiveTestResponse> Screenshot(string run_id, string instance, CancellationToken cancellationToken);
    Task<LiveTestResponse> ScreenshotStatus(string run_id, string instance, string capture_id, CancellationToken cancellationToken);
    Task<LiveTestResponse> OptionsMenu(string run_id, string instance, string action, CancellationToken cancellationToken, string tab = null);
    Task<LiveTestResponse> UiLayers(string run_id, string instance, CancellationToken cancellationToken);
    Task<LiveTestResponse> UiInspect(string run_id, string instance, CancellationToken cancellationToken, string snapshot = null, int offset = 0, string layer = null);
    Task<LiveTestResponse> UiAction(string run_id, string instance, string snapshot, string element, string action, CancellationToken cancellationToken, string text = null, double? value = null);
    Task<RunView> StopRun(string run_id);
}

[McpServerToolType]
public sealed class DebugTools : IDebugTools
{
    private readonly IRunOrchestrator runs;
    public DebugTools(IRunOrchestrator runs) { this.runs = runs; }

    [McpServerTool(Name = "start_run", UseStructuredContent = true), Description("Launch a configured in-game server and 0..16 deferred clients. Returns booting, NOT connected or ready. Next wait for server readyForCampaignTests, each client readyToJoin, join_client each, then wait each client readyForCampaignTests. Only one active run per MCP session.")]
    public Task<RunView> StartRun(string profile, int client_count, CancellationToken cancellationToken) =>
        runs.StartAsync(profile, client_count, cancellationToken);

    [McpServerTool(Name = "get_run", ReadOnly = true, UseStructuredContent = true), Description("Refresh owned instances: process alive is distinct from endpoint readiness, campaign readiness and mission readiness. Includes endpoint status and diagnostic errors.")]
    public Task<RunView> GetRun(string run_id, CancellationToken cancellationToken) => runs.GetAsync(run_id, cancellationToken);

    [McpServerTool(Name = "wait_for_state", ReadOnly = true, UseStructuredContent = true), Description("Bounded wait, 1..300 seconds, for one instance state: controlReady, readyToJoin, commandRegistryReady, readyForCampaignTests, readyForMissionTests, exited. Returns reached=false and diagnostics on timeout or early exit. No mutation retries.")]
    public Task<object> WaitForState(string run_id, string instance, string state, int timeout_seconds, CancellationToken cancellationToken) =>
        runs.WaitAsync(run_id, instance, state, timeout_seconds, cancellationToken);

    [McpServerTool(Name = "list_commands", ReadOnly = true, UseStructuredContent = true), Description("List ALL registered co-op framework commands plus legacy coop.debug.* commands for this instance. Wait for commandRegistryReady for the full session registry. Arbitrary vanilla console commands are not exposed.")]
    public Task<LiveTestResponse> ListCommands(string run_id, string instance, CancellationToken cancellationToken) =>
        runs.RequestAsync(run_id, instance, "command-catalog", new { }, false, cancellationToken);

    [McpServerTool(Name = "execute_command", UseStructuredContent = true), Description("Execute a catalog command on the target game thread. arguments is a string array, not a shell or console line. Preserve argument boundaries; do not add quoting. Run authoritative mutations on server, read-only inspections on clients. Inspect ok/error and output; outcomeUncertain=true means it may still execute, never blindly retry.")]
    public Task<LiveTestResponse> ExecuteCommand(string run_id, string instance, string name, string[] arguments, CancellationToken cancellationToken) =>
        runs.RequestAsync(run_id, instance, "command", new { name, arguments }, true, cancellationToken);

    [McpServerTool(Name = "join_client", UseStructuredContent = true), Description("Attempt one deferred client join after server readyForCampaignTests and client readyToJoin. This schedules joining, not completion. Then wait for client readyForCampaignTests. Do not retry uncertain outcomes.")]
    public Task<LiveTestResponse> JoinClient(string run_id, string instance, CancellationToken cancellationToken) =>
        runs.RequestAsync(run_id, instance, "join", new { }, true, cancellationToken);

    [McpServerTool(Name = "read_logs", ReadOnly = true, UseStructuredContent = true), Description("Read 4..65536 UTF-8 bytes from the endpoint-reported log (or archived log after stop). Omit cursor for beginning. Pass returned cursor for incremental reads. reset=true means replacement/truncation/compaction changed the observed content; output restarts at beginning. Cursors are opaque and instance-specific.")]
    public Task<LogChunk> ReadLogs(string run_id, string instance, CancellationToken cancellationToken, string cursor = null, int max_bytes = 16384) =>
        runs.ReadLogsAsync(run_id, instance, cursor, max_bytes, cancellationToken);

    [McpServerTool(Name = "screenshot", UseStructuredContent = true), Description("Request BMP screenshot in the run artifact directory. This is asynchronous; poll screenshot_status with returned captureId until complete. No automatic retry.")]
    public Task<LiveTestResponse> Screenshot(string run_id, string instance, CancellationToken cancellationToken) =>
        runs.RequestAsync(run_id, instance, "screenshot", new { path = runs.ScreenshotPath(run_id, instance) }, true, cancellationToken);

    [McpServerTool(Name = "screenshot_status", ReadOnly = true, UseStructuredContent = true), Description("Check an existing screenshot captureId; complete=true means the bridge observed a stable BMP file. Returns a local artifact path, not image bytes.")]
    public Task<LiveTestResponse> ScreenshotStatus(string run_id, string instance, string capture_id, CancellationToken cancellationToken) =>
        runs.RequestAsync(run_id, instance, "screenshot-status", new { captureId = capture_id }, false, cancellationToken);

    [McpServerTool(Name = "options_menu", UseStructuredContent = true), Description("Client-only co-op options navigation: open from campaign map, select tab id, inspect, or close WITHOUT applying. Omit tab on open to select the first available tab. Only controls its own topmost options screen; never clicks arbitrary UI or edits settings. Inspect ok/error; do not retry uncertain mutations. Use screenshot and screenshot_status to view the rendered menu.")]
    public Task<LiveTestResponse> OptionsMenu(string run_id, string instance, string action, CancellationToken cancellationToken, string tab = null)
    {
        if (action != "open" && action != "select" && action != "inspect" && action != "close")
            throw new ArgumentException("action must be open, select, inspect, or close", nameof(action));
        if ((action == "select" && string.IsNullOrEmpty(tab)) || (tab != null && tab.Length > 64))
            throw new ArgumentException("tab must contain 1..64 characters", nameof(tab));
        return runs.RequestAsync(run_id, instance, "options-menu", new { action, tab }, action != "inspect", cancellationToken);
    }

    [McpServerTool(Name = "ui_layers", ReadOnly = true, UseStructuredContent = true), Description("Discover up to 128 client UI layers without traversing widget descendants. Returns opaque layer handles in ascending stack order (last is highest). Handles expire after 30 seconds or layer/modal-root changes. Discovery invalidates previous widget snapshots. Selectable does not promise an actionable domain; inspect the selected layer next. Requires bridge uiCapability bounded-ui-layers-v1.")]
    public Task<LiveTestResponse> UiLayers(string run_id, string instance, CancellationToken cancellationToken) =>
        runs.RequestAsync(run_id, instance, "ui-layers", new { }, false, cancellationToken);

    [McpServerTool(Name = "ui_inspect", ReadOnly = true, UseStructuredContent = true), Description("Inspect native client UI, pages of 128, maximum 16384 widgets/depth 48. Omit snapshot and layer for compatible all-layer inspection. To scope explicitly, pass a fresh opaque layer from ui_layers on a new snapshot; the complete UIContext root plus relevant upper-layer blocking domains must fit the shared widget budget. Page with snapshot and nextOffset. ScopeComplete=false or truncated=true disables actions. Passwords are redacted. Snapshots expire after 30 seconds, selection/screen/modal changes or one action.")]
    public Task<LiveTestResponse> UiInspect(string run_id, string instance, CancellationToken cancellationToken, string snapshot = null, int offset = 0, string layer = null)
    {
        if (offset < 0 || offset > 16384 || (snapshot == null && offset != 0) ||
            (snapshot != null && snapshot.Length != 32) || (layer != null && layer.Length != 32))
            throw new ArgumentException("Use an observed snapshot/layer handle and a valid page offset.");
        return runs.RequestAsync(run_id, instance, "ui-inspect", new { snapshot, offset, layer }, false, cancellationToken);
    }

    [McpServerTool(Name = "ui_action", UseStructuredContent = true), Description("One-shot native client UI action on a freshly revalidated snapshot/element. click is semantic ButtonWidget.HandleClick activation, NOT physical mouse press/release, hover, focus or controller input. Exact ButtonWidget only (toggle value 0/1); unknown subclasses rejected. Selected-layer snapshots support only click/toggle. All-layer snapshots also retain text (max 512), slider (native range), scroll_vertical/scroll_horizontal (0..1). Use only advertised actions. Bounds cover bridge preflight, not native gameplay callbacks. Native callbacks may replace modals; no bridge tree read or cancellation follows activation. References consumed; never retry outcomeUncertain. Re-inspect and capture afterward. No selectors, VM calls or OS input.")]
    public Task<LiveTestResponse> UiAction(string run_id, string instance, string snapshot, string element, string action, CancellationToken cancellationToken, string text = null, double? value = null)
    {
        if (snapshot == null || snapshot.Length != 32 || element == null || element.Length > 16 ||
            !element.StartsWith("e", StringComparison.Ordinal) || !int.TryParse(element.Substring(1), out int index) || index < 0 ||
            (text != null && text.Length > 512) || (value.HasValue && !double.IsFinite(value.Value)))
            throw new ArgumentException("Use observed snapshot/element references and bounded text or finite value.");
        if (action != "click" && action != "toggle" && action != "text" && action != "slider" &&
            action != "scroll_vertical" && action != "scroll_horizontal")
            throw new ArgumentException("Unsupported UI action.", nameof(action));
        return runs.RequestAsync(run_id, instance, "ui-action", new { snapshot, element, action, text, value }, true, cancellationToken);
    }

    [McpServerTool(Name = "stop_run", UseStructuredContent = true), Description("Shutdown only owned processes, then force-stop those still alive after bounded grace. Archive endpoint-reported logs and retain all run artifacts. Idempotent; cleanup failures remain inspectable and can be retried.")]
    public Task<RunView> StopRun(string run_id) => runs.StopAsync(run_id);
}
