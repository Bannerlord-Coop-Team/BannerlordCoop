#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ScreenSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.LiveTesting;

/// <summary>Native widget boundary, called only on the game thread.</summary>
public interface IUiWidgetAdapter
{
    UiLayerStack Discover();
    UiFrame Read(object selectedLayer = null);
    void Act(UiWidget target, string action, string text, double? value, bool scoped = false);
}

/// <summary>Adapts rendered Gauntlet widgets, never VM members or operating-system input.</summary>
public sealed class GauntletWidgetAdapter : IUiWidgetAdapter
{
    private const int MaximumWidgets = LiveTestUi.MaximumWidgets;
    private const int MaximumDepth = 48;
    public const int MaximumLayers = 128;
    public const int MaximumRoots = 128;

    public UiLayerStack Discover()
    {
        var screen = ScreenManager.TopScreen;
        var stack = new UiLayerStack { Screen = screen, ScreenName = Limit(screen?.GetType().Name), Focus = ScreenManager.FocusedLayer };
        // SortedLayers rebuilds and sorts eagerly; bound its inputs before calling the getter.
        if ((screen?.Layers.Count ?? 0) + (ScreenManager._globalLayers?.Count ?? 0) > MaximumLayers)
        {
            stack.Truncated = true;
            return stack;
        }
        var layers = ScreenManager.SortedLayers;
        if (layers.Count > MaximumLayers) { stack.Truncated = true; return stack; }
        foreach (var layer in layers)
        {
            if (layer == null) { stack.Truncated = true; break; }
            var gauntlet = layer as GauntletLayer;
            var root = gauntlet?.UIContext?.Root;
            var state = new UiLayerState
            {
                Native = layer, Context = gauntlet?.UIContext, Root = root,
                Active = layer.IsActive, Finalized = layer.IsFinalized, FocusLayer = layer.IsFocusLayer,
                InputMask = (int)layer.InputUsageMask, Order = layer.InputRestrictions.Order,
                Name = Limit(layer.Name), Type = Limit(layer.GetType().Name),
                Supported = layer.GetType() == typeof(GauntletLayer) && root != null,
                RootCount = root?.ChildCount ?? 0,
                RootVisible = root != null && root.IsVisible && !root.DisableRender,
                RootEnabled = root != null && root.IsEnabled,
            };
            stack.Layers.Add(state);
            if ((gauntlet?._movieIdentifiers?.Count ?? 0) > MaximumRoots)
            { stack.Truncated = true; break; }
            if (root != null)
                for (int i = 0; i < root.ChildCount && i < MaximumRoots; i++)
                {
                    var child = root.GetChild(i);
                    state.Roots.Add(child);
                    state.RootStates.Add(child.IsVisible && !child.DisableRender);
                    state.RootStates.Add(child.IsEnabled);
                }
            if (gauntlet?._movieIdentifiers != null)
                foreach (var movie in gauntlet._movieIdentifiers)
                {
                    state.Movies.Add(movie);
                    state.Movies.Add(movie.Movie?.RootWidget);
                }
        }
        return stack;
    }

    public UiFrame Read(object selectedLayer = null)
    {
        var stack = Discover();
        var result = new UiFrame { Screen = stack.Screen, ScreenName = stack.ScreenName, Stack = stack, Truncated = stack.Truncated };
        if (result.Truncated || stack.Screen == null) return result;
        int selected = selectedLayer == null ? -1 : stack.Layers.FindIndex(l => ReferenceEquals(l.Native, selectedLayer));
        if (selectedLayer != null && selected < 0)
            throw new UiAutomationException("stale_reference", "Selected layer is no longer on the screen.");
        var domain = new UiFrame();
        var contexts = new HashSet<object>();
        for (int i = 0; i < stack.Layers.Count; i++)
        {
            var state = stack.Layers[i];
            if (!state.Active || state.Finalized) continue;
            bool inspected = selected < 0 || i == selected;
            bool blocker = i > selected && BlocksMouse(state);
            if (!inspected && !blocker) continue;
            if (!state.Supported)
            {
                if (blocker) result.ScopeComplete = false;
                continue;
            }
            var layer = (GauntletLayer)state.Native;
            // Shared contexts make the layer boundary ambiguous, even when a second layer is below selection.
            if (stack.Layers.Any(s => !ReferenceEquals(s.Native, layer) && ReferenceEquals(s.Context, state.Context)))
                result.ScopeComplete = false;
            if (!contexts.Add(state.Context)) { result.ScopeComplete = false; continue; }
            var destination = inspected ? result : domain;
            Visit(layer.UIContext.Root, layer, -1, 0, 0, true, true, false, destination,
                MaximumWidgets - (inspected ? domain.Widgets.Count : result.Widgets.Count));
            if (destination.Truncated) { result.Truncated = true; break; }
            var natives = new HashSet<object>(destination.Widgets.Where(w => ReferenceEquals(w.Layer, layer)).Select(w => w.Native));
            if (layer.UIContext.Root.ParentWidget != null ||
                destination.Widgets.Any(w => ReferenceEquals(w.Layer, layer) &&
                    (!ReferenceEquals(((Widget)w.Native).Context, layer.UIContext) ||
                     !ReferenceEquals(((Widget)w.Native).EventManager, layer.UIContext.EventManager))) ||
                state.Movies.Where((_, index) => index % 2 == 1).Any(root => root == null || !natives.Contains(root)))
                result.ScopeComplete = false;
        }
        if (selected >= 0)
            foreach (var item in result.Widgets)
                item.Actions = item.Redacted ? Array.Empty<string>() : Actions((Widget)item.Native, scoped: true);
        result.DomainWidgets = result.Widgets.Count + domain.Widgets.Count;
        result.OcclusionWidgets = domain.Widgets;
        // Only complete context roots and relevant upper layers may enter unbudgeted native hit testing.
        if (!result.Truncated && result.ScopeComplete)
            foreach (var item in result.Widgets)
                item.HitTestable = item.Visible && item.Enabled && item.Actions.Length > 0 &&
                    CanHit((Widget)item.Native, (GauntletLayer)item.Layer, Center((Widget)item.Native), stack);
        return result;
    }

    private bool BlocksMouse(UiLayerState layer) =>
        (layer.InputMask & (int)(InputUsageMask.MouseButtons | InputUsageMask.MouseWheels)) != 0;

    private void Visit(Widget widget, GauntletLayer layer, int parent, int childIndex, int depth,
        bool visible, bool enabled, bool redacted, UiFrame frame, int budget)
    {
        if (frame.Widgets.Count >= budget || depth > MaximumDepth)
        {
            frame.Truncated = true;
            return;
        }
        visible &= widget.IsVisible && !widget.DisableRender;
        enabled &= widget.IsEnabled;
        redacted |= widget is EditableTextWidget input && input.IsObfuscationEnabled;
        var point = Center(widget);
        var actions = Actions(widget);
        var label = redacted ? null : Label(widget);
        var value = redacted ? null : Value(widget);
        var item = new UiWidget
        {
            Native = widget, Layer = layer, Parent = parent, ChildIndex = childIndex,
            Id = Limit(widget.Id), Type = Limit(widget.GetType().Name), Redacted = redacted,
            Text = Limit(label), Value = Limit(value),
            ContentTruncated = label?.Length > 128 || value?.Length > 128,
            X = widget.GlobalPosition.X, Y = widget.GlobalPosition.Y, Width = widget.Size.X, Height = widget.Size.Y,
            Visible = visible && InsideClips(widget, point), Enabled = enabled,
            Actions = redacted ? Array.Empty<string>() : actions,
        };
        if (widget is SliderWidget numeric)
        {
            item.Minimum = numeric.MinValueFloat;
            item.Maximum = numeric.MaxValueFloat;
            item.Step = numeric.IsDiscrete ? numeric.DiscreteIncrementInterval : (float?)null;
        }
        int index = frame.Widgets.Count;
        frame.Widgets.Add(item);
        if (depth == MaximumDepth && widget.ChildCount > 0)
        {
            frame.Truncated = true;
            return;
        }
        for (int i = 0; i < widget.ChildCount; i++)
        {
            if (frame.Widgets.Count >= budget) { frame.Truncated = true; break; }
            Visit(widget.GetChild(i), layer, index, i, depth + 1, visible, enabled, redacted, frame, budget);
            if (frame.Truncated) break;
        }
    }

    internal string[] Actions(Widget widget, bool scoped = false)
    {
        if (widget.GetType() == typeof(ButtonWidget))
            return ((ButtonWidget)widget).IsToggle ? new[] { "click", "toggle" } : new[] { "click" };
        if (scoped) return Array.Empty<string>();
        if (widget.GetType() == typeof(EditableTextWidget)) return new[] { "text" };
        if (widget is SliderWidget slider && !slider.Locked) return new[] { "slider" };
        if (widget is ScrollablePanel panel)
        {
            var actions = new List<string>();
            if (panel.VerticalScrollbar != null) actions.Add("scroll_vertical");
            if (panel.HorizontalScrollbar != null) actions.Add("scroll_horizontal");
            return actions.ToArray();
        }
        return Array.Empty<string>();
    }

    public void Act(UiWidget target, string action, string text, double? value, bool scoped = false)
    {
        var widget = (Widget)target.Native;
        var layer = (GauntletLayer)target.Layer;
        if (!Actions(widget, scoped).Contains(action))
            throw new UiAutomationException("unsupported_action", "This concrete native control does not support that action in this scope.");
        var current = Read(scoped ? layer : null);
        if (current.Truncated || !current.ScopeComplete || !current.Widgets.Any(w =>
            ReferenceEquals(w.Native, widget) && w.Visible && w.Enabled && w.HitTestable && !w.Redacted && !w.ContentTruncated))
            throw new UiAutomationException("not_interactable", "Native hit test no longer reaches this control.");
        if (action == "click" || action == "toggle")
        {
            var button = (ButtonWidget)widget;
            if (action == "toggle")
            {
                if (value != 0 && value != 1) throw new UiAutomationException("invalid_parameters", "Toggle value must be zero or one.");
                if (button.IsSelected == (value == 1)) return;
            }
            ActivateButton((ButtonWidget)widget);
        }
        else if (action == "text")
        {
            var editable = (EditableTextWidget)widget;
            if (text == null || (editable.MaxLength >= 0 && text.Length > editable.MaxLength) ||
                text.Any(c => char.IsControl(c) || c == '<' || c == '>'))
                throw new UiAutomationException("invalid_parameters", "Text exceeds the native limit or contains unsupported characters.");
            RequireAvailableFocus(widget.EventManager);
            try
            {
                widget.EventManager.FocusedWidget = widget;
                editable.RealText = text;
            }
            finally
            {
                // Native RealText notifies bindings; clearing focus commits blur-based fields.
                widget.EventManager.ClearFocus();
            }
        }
        else if (action == "slider")
        {
            var slider = (SliderWidget)widget;
            var number = Number(value, slider.MinValueFloat, slider.MaxValueFloat);
            if (slider.IsPressed) throw new UiAutomationException("input_busy", "Release the slider before automation.");
            RequireAvailableFocus(widget.EventManager);
            try
            {
                widget.EventManager.FocusedWidget = widget;
                slider.ValueFloat = number;
            }
            finally
            {
                widget.EventManager.ClearFocus();
            }
        }
        else
        {
            var panel = (ScrollablePanel)widget;
            bool vertical = action == "scroll_vertical";
            var scrollbar = vertical ? panel.VerticalScrollbar : panel.HorizontalScrollbar;
            if (scrollbar == null) throw new UiAutomationException("unsupported_action", "Panel has no scrollbar for this axis.");
            float fraction = Number(value, 0, 1);
            float destination = scrollbar.MinValue + ((scrollbar.MaxValue - scrollbar.MinValue) * fraction);
            if (vertical) panel.SetVerticalScrollTarget(destination, 0);
            else panel.SetHorizontalScrollTarget(destination, 0);
        }
    }

    private void RequireAvailableFocus(EventManager events)
    {
        if (events.IsControllerActive || events._isOnScreenKeyboardRequested || TaleWorlds.InputSystem.Input.IsOnScreenKeyboardActive)
            throw new UiAutomationException("input_busy", "Release controller and on-screen keyboard input before automation.");
    }

    // Semantic native activation, not a simulated pointer gesture or a VM/event-name dispatch.
    internal void ActivateButton(ButtonWidget button)
    {
        if (button.GetType() != typeof(ButtonWidget))
            throw new UiAutomationException("unsupported_action", "Only the exact native ButtonWidget supports semantic activation.");
        var events = button.EventManager;
        if (events._mouseIsDown || events._mouseAlternateIsDown || events.IsDragging || button.IsPressed ||
            button._clickState != ButtonWidget.ButtonClickState.None)
            throw new UiAutomationException("input_busy", "Release native input before UI automation.");
        try
        {
            button.HandleClick();
        }
        catch (Exception exception)
        {
            // Even a bridge-shaped exception from a native callback follows possible mutation.
            throw new InvalidOperationException("Native button activation failed; outcome is uncertain. Do not retry.", exception);
        }
    }

    private bool CanHit(Widget widget, GauntletLayer layer, Vector2 point, UiLayerStack stack)
    {
        if (!InsideClips(widget, point)) return false;
        for (int i = stack.Layers.Count - 1; i >= 0; i--)
        {
            var above = stack.Layers[i];
            if (ReferenceEquals(above.Native, layer)) break;
            // Maskless layers cannot block; native ScreenManager would still traverse their trees.
            if (above.Active && !above.Finalized && BlocksMouse(above) &&
                (!above.Supported || ((GauntletLayer)above.Native).HitTest(point))) return false;
        }
        var kind = widget is ScrollablePanel ? GauntletEvent.MouseScroll : GauntletEvent.MousePressed;
        var hit = widget.EventManager.GetWidgetAtPositionForEvent(kind, point);
        if (widget is ScrollablePanel)
        {
            for (int i = 0; hit != null && i <= MaximumDepth; i++, hit = hit.ParentWidget)
                if (hit == widget) return true;
            return false;
        }
        return hit == widget;
    }

    private bool InsideClips(Widget widget, Vector2 point)
    {
        if (widget.Size.X <= 0 || widget.Size.Y <= 0 || !Finite(point.X) || !Finite(point.Y)) return false;
        var page = widget.EventManager.PageSize;
        if (point.X < 0 || point.Y < 0 || point.X >= page.X || point.Y >= page.Y) return false;
        int depth = 0;
        for (var parent = widget.ParentWidget; parent != null; parent = parent.ParentWidget)
        {
            if (++depth > MaximumDepth || !parent.IsVisible || parent.DisableRender) return false;
            var start = parent.GlobalPosition;
            if ((parent.ClipContents || parent.ClipHorizontalContent) && (point.X < start.X || point.X >= start.X + parent.Size.X)) return false;
            if ((parent.ClipContents || parent.ClipVerticalContent) && (point.Y < start.Y || point.Y >= start.Y + parent.Size.Y)) return false;
            if (parent.CircularClipEnabled) return false;
        }
        return true;
    }

    private Vector2 Center(Widget widget) => widget.GlobalPosition + (widget.Size / 2);
    private bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private float Number(double? value, float min, float max)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value < min || value > max)
            throw new UiAutomationException("invalid_parameters", "Value is outside the native control range.");
        return (float)value.Value;
    }
    private string Limit(string text) => text == null ? null : text.Substring(0, Math.Min(128, text.Length));
    private string Value(Widget widget)
    {
        if (widget is EditableTextWidget editable) return editable.RealText;
        if (widget is SliderWidget slider) return slider.ValueFloat.ToString(CultureInfo.InvariantCulture);
        if (widget is ButtonWidget button && (button.IsRadio || button.IsToggle)) return button.IsSelected ? "true" : "false";
        if (widget is ScrollablePanel panel) return string.Format(CultureInfo.InvariantCulture,
            "vertical={0};horizontal={1}", panel.VerticalScrollbar?.ValueFloat, panel.HorizontalScrollbar?.ValueFloat);
        return null;
    }
    private string Label(Widget widget)
    {
        if (widget is EditableTextWidget) return null;
        if (widget is TextWidget text) return text.Text;
        if (widget is RichTextWidget rich) return rich.Text;
        if (!(widget is ButtonWidget)) return null;
        var pending = new Queue<Widget>();
        pending.Enqueue(widget);
        for (int count = 0; pending.Count > 0 && count < 32; count++)
        {
            var next = pending.Dequeue();
            if (!next.IsVisible || next is EditableTextWidget) continue;
            if (next is TextWidget childText) return childText.Text;
            if (next is RichTextWidget childRich) return childRich.Text;
            for (int i = 0; i < next.ChildCount && pending.Count < 32; i++) pending.Enqueue(next.GetChild(i));
        }
        return null;
    }
}
#endif
