#if DEBUG
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;
using System.Runtime.Serialization;
using GameInterface.Services.LiveTesting;
using Moq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.GauntletInput;
using TaleWorlds.InputSystem;
using TaleWorlds.TwoDimension;
using Xunit;

namespace GameInterface.Tests.Services.LiveTesting;

[CollectionDefinition(nameof(GauntletAutomationCollection), DisableParallelization = true)]
public sealed class GauntletAutomationCollection { }

[Collection(nameof(GauntletAutomationCollection))]
public class GauntletWidgetAdapterTests
{
    private readonly GauntletWidgetAdapter adapter = new GauntletWidgetAdapter();

    private UIContext Context()
    {
        WidgetInfo.Refresh();
        var platform = new Mock<ITwoDimensionPlatform>();
        platform.SetupGet(p => p.ReferenceWidth).Returns(1920);
        platform.SetupGet(p => p.ReferenceHeight).Returns(1080);
        var context = new UIContext(new TwoDimensionContext(platform.Object, null, null), new Mock<IInputContext>().Object);
        context.BrushFactory = (BrushFactory)FormatterServices.GetUninitializedObject(typeof(BrushFactory));
        context.BrushFactory._brushes = new Dictionary<string, Brush> { ["DefaultBrush"] = new Brush() };
        context.FontFactory = new FontFactory(new TaleWorlds.Library.ResourceDepot());
        context.FontFactory._currentLangugage = new Language { DefaultFont = new Font("test"), LanguageID = "English" };
        context.EventManager = new EventManager(context);
        context.EventManager.PageSize = new Vector2(1920, 1080);
        context.Root.Size = new Vector2(1920, 1080);
        return context;
    }

    [Fact]
    public void NativeToggleClickEmitsBindingChangeAndClickEvent()
    {
        var context = Context();
        var button = new ButtonWidget(context) { ButtonType = ButtonType.Toggle };
        button.Size = new Vector2(40, 40);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        bool backingOption = false;
        int clicks = 0;
        button.boolPropertyChanged += (_, name, value) => { if (name == "IsSelected") backingOption = (bool)value; };
        button.ClickEventHandlers.Add(_ => clicks++);
        adapter.Click(button, new Vector2(10, 10));
        Assert.True(button.IsSelected);
        Assert.True(backingOption);
        Assert.Equal(1, clicks);
        Assert.False(context._uiInputContext._isMousePositionOverridden);
        Assert.False(context.EventManager._mouseIsDown);
    }

    [Fact]
    public void ThrowingNativeClickRestoresExistingMouseOverride()
    {
        var context = Context();
        var button = new ButtonWidget(context);
        button.Size = new Vector2(40, 40);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        context._uiInputContext.SetMousePositionOverride(new Vector2(70, 80));
        button.ClickEventHandlers.Add(_ => throw new InvalidOperationException("native callback"));
        Assert.Throws<InvalidOperationException>(() => adapter.Click(button, new Vector2(10, 10)));
        Assert.True(context._uiInputContext._isMousePositionOverridden);
        Assert.Equal(new Vector2(70, 80), context._uiInputContext.GetMousePosition());
        Assert.False(context.EventManager._mouseIsDown);
        Assert.Null(context.EventManager.OnGetIsHitThisFrame);
    }

    [Fact]
    public void NativeTextAndSliderEmitRealBindingNotifications()
    {
        var context = Context();
        var text = new EditableTextWidget(context) { Size = new Vector2(200, 40), MaxLength = 20 };
        var slider = new SliderWidget(context) { Size = new Vector2(200, 40), Top = 60, MinValueFloat = 0, MaxValueFloat = 1 };
        context.Root.AddChild(text);
        context.Root.AddChild(slider);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        string backingText = "";
        string visibleTextBinding = "";
        float backingValue = 0;
        text.PropertyChanged += (_, name, value) =>
        {
            if (name == "RealText") backingText = (string)value;
            if (name == "Text") visibleTextBinding = (string)value;
        };
        slider.floatPropertyChanged += (_, name, value) => { if (name == "ValueFloat") backingValue = value; };
        adapter.Act(new UiWidget { Native = text, Layer = screen.Layer }, "text", "Danustica", null);
        adapter.Act(new UiWidget { Native = slider, Layer = screen.Layer }, "slider", null, 0.75);
        Assert.Equal("Danustica", text.RealText);
        Assert.Equal("Danustica", backingText);
        Assert.Equal("Danustica", visibleTextBinding);
        Assert.Equal(0.75f, backingValue);
        Assert.Null(context.EventManager.FocusedWidget);
    }

    [Theory]
    [InlineData("too long")]
    [InlineData("<tag>")]
    [InlineData("line\nbreak")]
    public void InvalidNativeTextDoesNotChangeValueOrFocus(string replacement)
    {
        var context = Context();
        var text = new EditableTextWidget(context) { Size = new Vector2(200, 40), MaxLength = 5, RealText = "old" };
        context.Root.AddChild(text);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var error = Assert.Throws<UiAutomationException>(() =>
            adapter.Act(new UiWidget { Native = text, Layer = screen.Layer }, "text", replacement, null));
        Assert.Equal("invalid_parameters", error.Code);
        Assert.Equal("old", text.RealText);
        Assert.Null(context.EventManager.FocusedWidget);
    }

    [Fact]
    public void ThrowingNativeTextBindingClearsFocus()
    {
        var context = Context();
        var text = new EditableTextWidget(context) { Size = new Vector2(200, 40) };
        context.Root.AddChild(text);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        text.PropertyChanged += (_, name, _) => { if (name == "RealText") throw new InvalidOperationException("binding failed after write"); };
        Assert.Throws<InvalidOperationException>(() =>
            adapter.Act(new UiWidget { Native = text, Layer = screen.Layer }, "text", "changed", null));
        Assert.Equal("changed", text.RealText);
        Assert.Null(context.EventManager.FocusedWidget);
    }

    [Fact]
    public void NativeSliderPreservesDiscreteStepAndRejectsOutOfRange()
    {
        var context = Context();
        var slider = new SliderWidget(context)
        {
            Size = new Vector2(200, 40), MinValueFloat = 0, MaxValueFloat = 100,
            IsDiscrete = true, DiscreteIncrementInterval = 5, ValueFloat = 40,
        };
        context.Root.AddChild(slider);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var target = new UiWidget { Native = slider, Layer = screen.Layer };
        adapter.Act(target, "slider", null, 41);
        Assert.Equal(40, slider.ValueFloat);
        adapter.Act(target, "slider", null, 45);
        Assert.Equal(45, slider.ValueFloat);
        Assert.Throws<UiAutomationException>(() => adapter.Act(target, "slider", null, 101));
        Assert.Equal(45, slider.ValueFloat);
    }

    [Fact]
    public void NativeInspectionRedactsPasswordAndDescendants()
    {
        var context = Context();
        var password = new EditableTextWidget(context) { Size = new Vector2(200, 40), IsObfuscationEnabled = true, RealText = "secret123" };
        password.AddChild(new TextWidget(context) { Text = "secret123" });
        context.Root.AddChild(password);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var frame = adapter.Read();
        Assert.Equal(2, frame.Widgets.Count(n => n.Redacted));
        Assert.All(frame.Widgets.Where(n => n.Redacted), n => { Assert.Null(n.Text); Assert.Null(n.Value); Assert.Empty(n.Actions); });
        Assert.DoesNotContain("secret123", JsonSerializer.Serialize(frame.Widgets));
    }

    [Fact]
    public void NativeScrollablePanelUpdatesScrollbarThroughInterpolationController()
    {
        var context = Context();
        var panel = new ScrollablePanel(context) { Size = new Vector2(200, 200) };
        var bar = new ScrollbarWidget(context) { MinValue = 0, MaxValue = 500 };
        panel.VerticalScrollbar = bar;
        context.Root.AddChild(panel);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        adapter.Act(new UiWidget { Native = panel, Layer = screen.Layer }, "scroll_vertical", null, 0.5);
        panel._verticalScrollbarInterpolationController.Tick(0.016f);
        Assert.Equal(250, bar.ValueFloat);
    }

    [Fact]
    public void NativeInspectionReportsHiddenDisabledAndClippedControls()
    {
        var context = Context();
        var parent = new Widget(context) { Size = new Vector2(40, 40), ClipContents = true };
        var clipped = new ButtonWidget(context) { Size = new Vector2(40, 40), Top = 80 };
        var disabled = new ButtonWidget(context) { Size = new Vector2(40, 40), IsEnabled = false };
        var hidden = new ButtonWidget(context) { Size = new Vector2(40, 40), IsVisible = false };
        parent.AddChild(clipped); context.Root.AddChild(parent); context.Root.AddChild(disabled); context.Root.AddChild(hidden);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var frame = adapter.Read();
        Assert.False(frame.Widgets.Single(n => n.Native == clipped).Visible);
        Assert.False(frame.Widgets.Single(n => n.Native == disabled).Enabled);
        Assert.False(frame.Widgets.Single(n => n.Native == hidden).Visible);
        Assert.All(frame.Widgets.Where(n => n.Native == clipped || n.Native == disabled || n.Native == hidden), n => Assert.False(n.HitTestable));
    }

    [Theory]
    [InlineData(4100, false)]
    [InlineData(16400, true)]
    public void NativeTraversalRetainsLargeTreesAndStopsAtHardCap(int children, bool truncated)
    {
        var context = Context();
        for (int i = 0; i < children; i++) context.Root.AddChild(new Widget(context));
        using var screen = new TestScreenScope(context);
        var frame = adapter.Read();
        Assert.Equal(truncated, frame.Truncated);
        Assert.Equal(Math.Min(children + 1, LiveTestUi.MaximumWidgets), frame.Widgets.Count);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(LiveTestUi.MaximumWidgets, true)]
    public void VisibleTreeRunsNativeHitTestingOnlyAfterPassingBounds(int children, bool truncated)
    {
        var context = Context();
        var button = new HitCountingButton(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        for (int i = 0; i < children; i++)
            context.Root.AddChild(new Widget(context) { Size = new Vector2(40, 40), DoNotAcceptEvents = true });
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var frame = adapter.Read();
        Assert.Equal(truncated, frame.Truncated);
        Assert.Equal(Math.Min(children + 2, LiveTestUi.MaximumWidgets), frame.Widgets.Count);
        Assert.True(frame.Widgets.Single(n => n.Native == button).Visible);
        Assert.Equal(!truncated, frame.Widgets.Single(n => n.Native == button).HitTestable);
        Assert.Equal(truncated ? 0 : 1, button.HitTests);
        if (truncated) Assert.All(frame.Widgets, n => Assert.False(n.HitTestable));
    }

    [Fact]
    public void WideDepthLimitedTreeStopsBeforeChildrenAndRemainingSiblings()
    {
        var context = Context();
        var parent = context.Root;
        for (int depth = 1; depth <= 48; depth++)
        {
            var child = new Widget(context);
            parent.AddChild(child);
            parent = child;
        }
        for (int i = 0; i < 20000; i++) parent.AddChild(new Widget(context));
        // An unreadable child proves the depth boundary never enumerates its descendants.
        parent.Children[0] = null;
        var remaining = new Widget(context);
        context.Root.AddChild(remaining);
        using var screen = new TestScreenScope(context);
        var frame = adapter.Read();
        Assert.True(frame.Truncated);
        Assert.Equal(49, frame.Widgets.Count);
        Assert.DoesNotContain(frame.Widgets, n => n.Native == remaining);
        Assert.All(frame.Widgets, n => Assert.False(n.HitTestable));
    }

    [Theory]
    [InlineData("text", true, false)]
    [InlineData("text", false, true)]
    [InlineData("slider", true, false)]
    [InlineData("slider", false, true)]
    public void ControllerOrPendingKeyboardRejectsFocusActionsWithoutMutation(string action, bool controller, bool keyboardRequested)
    {
        var context = Context();
        var existingFocus = new Widget(context) { IsFocusable = true };
        var text = new EditableTextWidget(context) { Size = new Vector2(200, 40), RealText = "old" };
        var slider = new SliderWidget(context) { Size = new Vector2(200, 40), Top = 60, MinValueFloat = 0, MaxValueFloat = 1 };
        context.Root.AddChild(existingFocus);
        context.Root.AddChild(text);
        context.Root.AddChild(slider);
        context.Root.UpdatePosition();
        context.EventManager.FocusedWidget = existingFocus;
        context.EventManager.IsControllerActive = controller;
        context.EventManager._isOnScreenKeyboardRequested = keyboardRequested;
        using var screen = new TestScreenScope(context);
        var target = new UiWidget { Native = action == "text" ? (Widget)text : slider, Layer = screen.Layer };
        var error = Assert.Throws<UiAutomationException>(() => adapter.Act(target, action, "changed", 0.75));
        Assert.Equal("input_busy", error.Code);
        Assert.Equal("old", text.RealText);
        Assert.Equal(0, slider.ValueFloat);
        Assert.Same(existingFocus, context.EventManager.FocusedWidget);
        Assert.Equal(keyboardRequested, context.EventManager._isOnScreenKeyboardRequested);
        Assert.Equal(controller, context.EventManager.IsControllerActive);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("slider")]
    public void ActivePlatformKeyboardRejectsFocusActionsWithoutMutation(string action)
    {
        var context = Context();
        var text = new EditableTextWidget(context) { Size = new Vector2(200, 40), RealText = "old" };
        var slider = new SliderWidget(context) { Size = new Vector2(200, 40), Top = 60, MinValueFloat = 0, MaxValueFloat = 1 };
        context.Root.AddChild(text);
        context.Root.AddChild(slider);
        context.Root.UpdatePosition();
        context.EventManager.FocusedWidget = text;
        using var screen = new TestScreenScope(context);
        bool previousKeyboardActive = Input.IsOnScreenKeyboardActive;
        try
        {
            Input.IsOnScreenKeyboardActive = true;
            var target = new UiWidget { Native = action == "text" ? (Widget)text : slider, Layer = screen.Layer };
            var error = Assert.Throws<UiAutomationException>(() => adapter.Act(target, action, "changed", 0.75));
            Assert.Equal("input_busy", error.Code);
            Assert.Equal("old", text.RealText);
            Assert.Equal(0, slider.ValueFloat);
            Assert.Same(text, context.EventManager.FocusedWidget);
            Assert.False(context.EventManager._isOnScreenKeyboardRequested);
            Assert.True(Input.IsOnScreenKeyboardActive);
        }
        finally
        {
            Input.IsOnScreenKeyboardActive = previousKeyboardActive;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TruncatedNativeTextCannotBeOverwrittenUsingSnapshot(bool changeTail)
    {
        var context = Context();
        var prefix = new string('a', 128);
        var text = new EditableTextWidget(context) { Size = new Vector2(200, 40), RealText = prefix + "x" };
        context.Root.AddChild(text);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0);
        var element = snapshot.Elements.Single(e => e.Widget.Native == text);
        Assert.Equal(prefix, element.Widget.Value);
        Assert.True(element.Widget.ContentTruncated);
        Assert.DoesNotContain("ContentTruncated", JsonSerializer.Serialize(snapshot));
        if (changeTail) text.RealText = prefix + "y";
        var error = Assert.Throws<UiAutomationException>(() => ui.Act(snapshot.Snapshot, element.Reference, "text", "replacement", null));
        Assert.Equal("not_interactable", error.Code);
        Assert.Equal(prefix + (changeTail ? "y" : "x"), text.RealText);
        Assert.Null(context.EventManager.FocusedWidget);
    }

    private sealed class HitCountingButton : ButtonWidget
    {
        public int HitTests { get; private set; }
        public HitCountingButton(UIContext context) : base(context) { }
        public override bool OnPreviewMousePressed()
        {
            HitTests++;
            return base.OnPreviewMousePressed();
        }
    }

    private sealed class TestScreen : ScreenBase { }
    private sealed class TestScreenScope : IDisposable
    {
        private readonly ScreenBase previousScreen = ScreenManager.TopScreen;
        private readonly List<ScreenLayer> previousLayers = ScreenManager._sortedLayers;
        private readonly ObservableCollection<GlobalLayer> previousGlobals = ScreenManager._globalLayers;
        public GauntletLayer Layer { get; }
        public TestScreenScope(UIContext context)
        {
            Layer = (GauntletLayer)FormatterServices.GetUninitializedObject(typeof(GauntletLayer));
            Layer.UIContext = context;
            Layer.IsActive = true;
            var screen = new TestScreen();
            screen._layers.Add(Layer);
            ScreenManager.TopScreen = screen;
            ScreenManager._sortedLayers = new List<ScreenLayer> { Layer };
            ScreenManager._globalLayers = new ObservableCollection<GlobalLayer>();
        }
        public void Dispose()
        {
            ScreenManager.TopScreen = previousScreen;
            ScreenManager._sortedLayers = previousLayers;
            ScreenManager._globalLayers = previousGlobals;
        }
    }

    [Fact]
    public void UnsupportedBaseWidgetHasNoActions()
    {
        Assert.Empty(adapter.Actions(new Widget(Context())));
    }
}
#endif
