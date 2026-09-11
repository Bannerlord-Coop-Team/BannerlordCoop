#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameInterface.Services.LiveTesting;

/// <summary>Bounded snapshot inspection and one-shot native UI actions.</summary>
public interface ILiveTestUi
{
    UiLayerSnapshot Discover();
    UiSnapshot Inspect(string snapshot, int offset, string layer = null);
    object Act(string snapshot, string element, string action, string text, double? value);
}

/// <summary>Owns references for one DEBUG bridge; all calls run on the game thread.</summary>
public sealed class LiveTestUi : ILiveTestUi
{
    public const int PageSize = 128;
    public const int MaximumWidgets = 16384;
    private readonly IUiWidgetAdapter adapter;
    private readonly Stopwatch age = new Stopwatch();
    private UiFrame frame;
    private string snapshotId;
    private readonly Stopwatch layerAge = new Stopwatch();
    private readonly Dictionary<string, object> layers = new Dictionary<string, object>();
    private UiLayerStack layerStack;
    private object selectedLayer;
    private string selectedHandle;

    public LiveTestUi(IUiWidgetAdapter adapter)
    {
        if (adapter == null) throw new ArgumentNullException(nameof(adapter));
        this.adapter = adapter;
    }

    public UiLayerSnapshot Discover()
    {
        snapshotId = null;
        layers.Clear();
        layerStack = adapter.Discover();
        layerAge.Restart();
        var result = new UiLayerSnapshot
        {
            Screen = layerStack.ScreenName, Truncated = layerStack.Truncated,
            MaximumLayers = GauntletWidgetAdapter.MaximumLayers,
        };
        for (int i = 0; i < layerStack.Layers.Count; i++)
        {
            var state = layerStack.Layers[i];
            bool selectable = !layerStack.Truncated && state.Supported && state.Active && !state.Finalized && state.RootCount <= GauntletWidgetAdapter.MaximumRoots;
            string handle = selectable ? Guid.NewGuid().ToString("N") : null;
            if (handle != null) layers.Add(handle, state.Native);
            result.Layers.Add(new UiLayerDescription
            {
                Layer = handle, Name = state.Name, Type = state.Type, StackIndex = i,
                Active = state.Active && !state.Finalized, Selectable = selectable, InputMask = state.InputMask,
                Visible = state.RootVisible, RootCount = state.RootCount,
            });
        }
        return result;
    }

    public UiSnapshot Inspect(string snapshot, int offset, string layer = null)
    {
        if (offset < 0 || offset > MaximumWidgets || (snapshot == null && offset != 0))
            throw new UiAutomationException("invalid_parameters", "Use offset zero for a new snapshot, or page an existing snapshot.");
        if (snapshot == null)
        {
            snapshotId = null;
            selectedLayer = null;
            selectedHandle = null;
            if (layer != null)
            {
                if (layer.Length != 32 || !layers.TryGetValue(layer, out selectedLayer))
                    throw new UiAutomationException("stale_reference", "Discover layers before selecting an opaque layer handle.");
                ValidateLayers();
                selectedHandle = layer;
            }
            frame = adapter.Read(selectedLayer);
            snapshotId = Guid.NewGuid().ToString("N");
            age.Restart();
        }
        else
        {
            if (layer != null && layer != selectedHandle)
                throw new UiAutomationException("invalid_parameters", "A snapshot's layer scope cannot be changed while paging.");
            Validate(snapshot);
        }
        var result = new UiSnapshot
        {
            Snapshot = snapshotId, Screen = frame.ScreenName, Total = frame.Widgets.Count,
            Truncated = frame.Truncated, NextOffset = Math.Min(offset + PageSize, frame.Widgets.Count),
            Layer = selectedHandle, ScopeComplete = !frame.Truncated && frame.ScopeComplete, DomainWidgets = frame.DomainWidgets,
        };
        for (int i = offset; i < frame.Widgets.Count && i < offset + PageSize; i++)
            result.Elements.Add(new UiElement { Reference = "e" + i, Widget = frame.Widgets[i] });
        return result;
    }

    public object Act(string snapshot, string element, string action, string text, double? value)
    {
        if (element == null || element.Length > 16 || !element.StartsWith("e", StringComparison.Ordinal) ||
            !int.TryParse(element.Substring(1), out int index) || index < 0)
            throw new UiAutomationException("invalid_parameters", "Expected an element reference from ui_inspect.");
        var current = Validate(snapshot);
        if (index >= frame.Widgets.Count)
            throw new UiAutomationException("stale_reference", "Element does not belong to this snapshot.");
        if (frame.Truncated || current.Truncated)
            throw new UiAutomationException("tree_truncated", "Tree exceeded traversal limits; actions are disabled.");
        if (!frame.ScopeComplete || !current.ScopeComplete)
            throw new UiAutomationException("incomplete_scope", "The complete native hit-test domain could not be bounded; actions are disabled.");
        var previous = frame.Widgets[index];
        var target = current.Widgets[index];
        if (!SameTarget(previous, target))
            throw new UiAutomationException("stale_reference", "Target content or geometry changed; inspect again.");
        if (previous.ContentTruncated || target.ContentTruncated)
            throw new UiAutomationException("not_interactable", "Target content exceeds inspection limits; actions are disabled.");
        if (target.Redacted || !target.Visible || !target.Enabled || !target.HitTestable)
            throw new UiAutomationException("not_interactable", "Target is hidden, clipped, disabled, covered or protected.");
        if (!target.Actions.Contains(action))
            throw new UiAutomationException("unsupported_action", "This native control does not support that action.");
        if ((text != null && text.Length > 512) || (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value))))
            throw new UiAutomationException("invalid_parameters", "Text is limited to 512 characters; numeric values must be finite.");
        // Consume before native dispatch: an exception may follow a successful UI side effect.
        snapshotId = null;
        layers.Clear();
        adapter.Act(target, action, text, value, selectedLayer != null);
        return new { dispatched = true, inspectAgain = true };
    }

    private void ValidateLayers()
    {
        if (layerAge.Elapsed > TimeSpan.FromSeconds(30) || !layerStack.Matches(adapter.Discover()))
        {
            layers.Clear();
            snapshotId = null;
            throw new UiAutomationException("stale_reference", "Layer stack or modal roots changed; discover layers again.");
        }
    }

    private UiFrame Validate(string snapshot)
    {
        if (snapshotId == null || snapshot != snapshotId || age.Elapsed > TimeSpan.FromSeconds(30))
            throw new UiAutomationException("stale_reference", "Snapshot expired; inspect again.");
        if (selectedLayer != null) ValidateLayers();
        var current = adapter.Read(selectedLayer);
        if ((frame.Stack != null && !frame.Stack.Matches(current.Stack)) ||
            !ReferenceEquals(frame.Screen, current.Screen) || frame.Truncated != current.Truncated ||
            frame.Widgets.Count != current.Widgets.Count)
            throw new UiAutomationException("stale_reference", "Screen or widget tree changed; inspect again.");
        if (selectedLayer != null)
        {
            if (frame.OcclusionWidgets.Count != current.OcclusionWidgets.Count)
                throw new UiAutomationException("stale_reference", "Upper-layer modal domain changed; inspect again.");
            for (int i = 0; i < frame.OcclusionWidgets.Count; i++)
            {
                var a = frame.OcclusionWidgets[i];
                var b = current.OcclusionWidgets[i];
                if (!ReferenceEquals(a.Native, b.Native) || !ReferenceEquals(a.Layer, b.Layer) ||
                    a.Parent != b.Parent || a.ChildIndex != b.ChildIndex || a.Id != b.Id ||
                    !SameTarget(a, b) || a.Visible != b.Visible || a.Enabled != b.Enabled)
                    throw new UiAutomationException("stale_reference", "Upper-layer modal domain changed; inspect again.");
            }
        }
        for (int i = 0; i < frame.Widgets.Count; i++)
        {
            var a = frame.Widgets[i];
            var b = current.Widgets[i];
            if (!ReferenceEquals(a.Native, b.Native) || !ReferenceEquals(a.Layer, b.Layer) ||
                a.Parent != b.Parent || a.ChildIndex != b.ChildIndex || a.Id != b.Id ||
                (selectedLayer != null && (!SameTarget(a, b) || a.Visible != b.Visible || a.Enabled != b.Enabled)))
                throw new UiAutomationException("stale_reference", "Widget hierarchy changed; inspect again.");
        }
        return current;
    }

    private bool SameTarget(UiWidget a, UiWidget b) =>
        a.Type == b.Type && a.Text == b.Text && a.Value == b.Value && a.Redacted == b.Redacted &&
        a.Minimum == b.Minimum && a.Maximum == b.Maximum && a.Step == b.Step &&
        Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1 &&
        Math.Abs(a.Width - b.Width) <= 1 && Math.Abs(a.Height - b.Height) <= 1;
}

/// <summary>A page from a snapshot, never containing native object references.</summary>
public sealed class UiSnapshot
{
    public string Snapshot { get; set; }
    public string Screen { get; set; }
    public int Total { get; set; }
    public int NextOffset { get; set; }
    public bool Truncated { get; set; }
    public string Layer { get; set; }
    public bool ScopeComplete { get; set; }
    public int DomainWidgets { get; set; }
    public List<UiElement> Elements { get; } = new List<UiElement>();
}

/// <summary>A snapshot-scoped reference and its displayed metadata.</summary>
public sealed class UiElement
{
    public string Reference { get; set; }
    public UiWidget Widget { get; set; }
}
#endif
