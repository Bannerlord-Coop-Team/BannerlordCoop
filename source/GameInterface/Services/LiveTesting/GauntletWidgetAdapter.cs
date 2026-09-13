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

namespace GameInterface.Services.LiveTesting;

/// <summary>Native widget boundary, called only on the game thread.</summary>
public interface IUiWidgetAdapter
{
    UiFrame Read();
    void Act(UiWidget target, string action, string text, double? value);
}

/// <summary>Adapts rendered Gauntlet widgets, never VM members or operating-system input.</summary>
public sealed class GauntletWidgetAdapter : IUiWidgetAdapter
{
    private const int MaximumWidgets = LiveTestUi.MaximumWidgets;
    private const int MaximumDepth = 48;

    public UiFrame Read()
    {
        var screen = ScreenManager.TopScreen;
        var result = new UiFrame { Screen = screen, ScreenName = Limit(screen?.GetType().Name) };
        if (screen == null) return result;
        foreach (var layer in ScreenManager.SortedLayers.OfType<GauntletLayer>())
        {
            if (!layer.IsActive || layer.IsFinalized || layer.UIContext?.Root == null) continue;
            Visit(layer.UIContext.Root, layer, -1, 0, 0, true, true, false, result);
            if (result.Truncated) break;
        }
        // Native hit testing traverses roots without a budget, so first bound every inspected tree.
        if (!result.Truncated)
        {
            foreach (var item in result.Widgets)
                item.HitTestable = item.Visible && item.Enabled && item.Actions.Length > 0 &&
                    CanHit((Widget)item.Native, (GauntletLayer)item.Layer, Center((Widget)item.Native));
        }
        return result;
    }

    private void Visit(Widget widget, GauntletLayer layer, int parent, int childIndex, int depth,
        bool visible, bool enabled, bool redacted, UiFrame frame)
    {
        if (frame.Widgets.Count >= MaximumWidgets || depth > MaximumDepth)
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
            if (frame.Widgets.Count >= MaximumWidgets) { frame.Truncated = true; break; }
            Visit(widget.GetChild(i), layer, index, i, depth + 1, visible, enabled, redacted, frame);
            if (frame.Truncated) break;
        }
    }

    internal string[] Actions(Widget widget)
    {
        if (widget is ButtonWidget button)
            return button.IsToggle ? new[] { "click", "toggle" } : new[] { "click" };
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

    public void Act(UiWidget target, string action, string text, double? value)
    {
        var widget = (Widget)target.Native;
        var layer = (GauntletLayer)target.Layer;
        var point = Center(widget);
        if (!CanHit(widget, layer, point))
            throw new UiAutomationException("not_interactable", "Native hit test no longer reaches this control.");
        if (action == "click" || action == "toggle")
        {
            var button = (ButtonWidget)widget;
            if (action == "toggle")
            {
                if (value != 0 && value != 1) throw new UiAutomationException("invalid_parameters", "Toggle value must be zero or one.");
                if (button.IsSelected == (value == 1)) return;
            }
            Click(widget, point);
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

    internal void Click(Widget widget, Vector2 point)
    {
        var context = widget.Context._uiInputContext;
        bool previousOverride = context._isMousePositionOverridden;
        var previousPosition = context._overrideMousePosition;
        var events = widget.EventManager;
        var previousHitTest = events.OnGetIsHitThisFrame;
        if (events._mouseIsDown || events._mouseAlternateIsDown || events.IsDragging)
            throw new UiAutomationException("input_busy", "Release native input before UI automation.");
        try
        {
            context.SetMousePositionOverride(point);
            // Native frame hit state describes the physical cursor, not this validated target.
            events.OnGetIsHitThisFrame = () => true;
            events.MouseDown();
            events.MouseUp();
        }
        finally
        {
            try
            {
                if (events._mouseIsDown) events.MouseUp(false);
            }
            finally
            {
                events.OnGetIsHitThisFrame = previousHitTest;
                if (previousOverride) context.SetMousePositionOverride(previousPosition);
                else context.ResetMousePositionOverride();
            }
        }
    }

    private bool CanHit(Widget widget, GauntletLayer layer, Vector2 point)
    {
        if (!InsideClips(widget, point) || ScreenManager.IsLayerBlockedAtPosition(layer, point)) return false;
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
