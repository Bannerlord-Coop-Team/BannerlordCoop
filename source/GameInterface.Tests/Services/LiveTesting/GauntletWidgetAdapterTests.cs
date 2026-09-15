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
        adapter.ActivateButton(button);
        Assert.True(button.IsSelected);
        Assert.True(backingOption);
        Assert.Equal(1, clicks);
        Assert.False(context._uiInputContext._isMousePositionOverridden);
        Assert.False(context.EventManager._mouseIsDown);
    }

    [Fact]
    public void ThrowingNativeActivationLeavesExistingMouseOverrideUntouched()
    {
        var context = Context();
        var button = new ButtonWidget(context);
        button.Size = new Vector2(40, 40);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        context._uiInputContext.SetMousePositionOverride(new Vector2(70, 80));
        button.ClickEventHandlers.Add(_ => throw new InvalidOperationException("native callback"));
        Assert.Throws<InvalidOperationException>(() => adapter.ActivateButton(button));
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
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        var probe = new HitCountingWidget(context) { Size = new Vector2(40, 40) };
        button.AddChild(probe);
        context.Root.AddChild(button);
        for (int i = 0; i < children; i++)
            context.Root.AddChild(new Widget(context) { Size = new Vector2(40, 40), DoNotAcceptEvents = true });
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var frame = adapter.Read();
        Assert.Equal(truncated, frame.Truncated);
        Assert.Equal(Math.Min(children + 3, LiveTestUi.MaximumWidgets), frame.Widgets.Count);
        Assert.True(frame.Widgets.Single(n => n.Native == button).Visible);
        Assert.Equal(!truncated, frame.Widgets.Single(n => n.Native == button).HitTestable);
        Assert.Equal(truncated ? 0 : 1, probe.HitTests);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedCompleteContextIgnoresHugeLowerOrMasklessUpperTree(bool upper)
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        var probe = new HitCountingWidget(context) { Size = new Vector2(40, 40) };
        button.AddChild(probe);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var background = Context();
        var container = new Widget(background);
        background.Root.AddChild(container);
        for (int i = 0; i < 20000; i++) container.AddChild(new Widget(background));
        container.Children[0] = null; // Any descendant traversal would fail, not merely run slowly.
        screen.AddLayer(background, upper, false);
        var ui = new LiveTestUi(adapter);
        var discovery = ui.Discover();
        Assert.False(discovery.Truncated);
        var handle = discovery.Layers[upper ? 0 : 1].Layer;
        var snapshot = ui.Inspect(null, 0, handle);
        Assert.True(snapshot.ScopeComplete);
        Assert.False(snapshot.Truncated);
        Assert.Equal(3, snapshot.Total);
        Assert.Equal(3, snapshot.DomainWidgets);
        var element = snapshot.Elements.Single(e => e.Widget.Native == button);
        Assert.True(element.Widget.HitTestable);
        int clicks = 0;
        button.ClickEventHandlers.Add(_ => clicks++);
        ui.Act(snapshot.Snapshot, element.Reference, "click", null, null);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void SharedContextWithUnselectedLayerDisablesNativeHitTesting()
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        var probe = new HitCountingWidget(context) { Size = new Vector2(40, 40) };
        button.AddChild(probe);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        screen.AddLayer(context, false, false);
        var frame = adapter.Read(screen.Layer);
        Assert.False(frame.ScopeComplete);
        Assert.Equal(0, probe.HitTests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpperNativeLayerBlocksLowerButtonAndRevalidatesDynamicCoverage(bool dynamic)
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        int clicks = 0;
        button.ClickEventHandlers.Add(_ => clicks++);
        using var screen = new TestScreenScope(context);
        var upper = Context();
        upper.Root.DoNotAcceptEvents = dynamic;
        upper.Root.UpdatePosition();
        screen.AddLayer(upper, true, true);
        var ui = new LiveTestUi(adapter);
        var handle = ui.Discover().Layers[0].Layer;
        var snapshot = ui.Inspect(null, 0, handle);
        var element = snapshot.Elements.Single(e => e.Widget.Native == button);
        Assert.True(snapshot.ScopeComplete);
        Assert.Equal(dynamic, element.Widget.HitTestable);
        upper.Root.DoNotAcceptEvents = false;
        Assert.Equal("not_interactable", Assert.Throws<UiAutomationException>(() =>
            ui.Act(snapshot.Snapshot, element.Reference, "click", null, null)).Code);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void RelevantUpperTreeSharesWidgetBudgetAndNeverEntersNativeHitTestingWhenTruncated()
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        var probe = new HitCountingWidget(context) { Size = new Vector2(40, 40) };
        button.AddChild(probe);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var upper = Context();
        for (int i = 0; i < LiveTestUi.MaximumWidgets; i++) upper.Root.AddChild(new Widget(upper));
        screen.AddLayer(upper, true, true);
        var frame = adapter.Read(screen.Layer);
        Assert.True(frame.Truncated);
        Assert.Equal(LiveTestUi.MaximumWidgets, frame.DomainWidgets);
        Assert.Equal(0, probe.HitTests);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("stack")]
    [InlineData("mask")]
    [InlineData("modal")]
    public void DiscoveryHandlesRejectChangedLayerStackOrModalRoots(string change)
    {
        var context = Context();
        var modal = new Widget(context);
        context.Root.AddChild(modal);
        using var screen = new TestScreenScope(context);
        var ui = new LiveTestUi(adapter);
        string handle = ui.Discover().Layers[0].Layer;
        if (change == "root") context.EventManager.Root = new Widget(context);
        if (change == "stack") screen.AddLayer(Context(), true, false);
        if (change == "mask") screen.Layer.InputRestrictions.SetInputRestrictions(true, TaleWorlds.Library.InputUsageMask.Mouse);
        if (change == "modal") modal.IsVisible = false;
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() => ui.Inspect(null, 0, handle)).Code);
    }

    [Fact]
    public void DiscoveryLayerLimitFailsBeforeSortedLayerOrWidgetTraversal()
    {
        using var screen = new TestScreenScope(Context());
        for (int i = 0; i < 128; i++) ScreenManager.TopScreen._layers.Add(screen.Layer);
        var ui = new LiveTestUi(adapter);
        Assert.True(ui.Discover().Truncated);
        Assert.Empty(ui.Discover().Layers);
        Assert.True(adapter.Read().Truncated);
    }

    [Fact]
    public void InvisibleSelectedRootCannotDispatchAndUnknownUpperLayerFailsClosed()
    {
        var context = Context();
        context.Root.IsVisible = false;
        context.Root.AddChild(new ButtonWidget(context) { Size = new Vector2(40, 40) });
        using var screen = new TestScreenScope(context);
        Assert.All(adapter.Read(screen.Layer).Widgets, w => Assert.False(w.HitTestable));
        context.Root.IsVisible = true;
        var unknown = new UnknownLayer();
        unknown.IsActive = true;
        unknown.InputRestrictions.SetInputRestrictions(true, TaleWorlds.Library.InputUsageMask.Mouse);
        ScreenManager.TopScreen._layers.Add(unknown);
        ScreenManager._isSortedActiveLayersDirty = true;
        Assert.False(adapter.Read(screen.Layer).ScopeComplete);
    }

    [Fact]
    public void SelectedSnapshotDispatchesOnceAndDiscoveryRefreshInvalidatesHandles()
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        int clicks = 0;
        button.ClickEventHandlers.Add(_ => clicks++);
        using var screen = new TestScreenScope(context);
        var ui = new LiveTestUi(adapter);
        var oldHandle = ui.Discover().Layers[0].Layer;
        var handle = ui.Discover().Layers[0].Layer;
        Assert.Throws<UiAutomationException>(() => ui.Inspect(null, 0, oldHandle));
        var snapshot = ui.Inspect(null, 0, handle);
        var element = snapshot.Elements.Single(e => e.Widget.Native == button);
        Assert.Throws<UiAutomationException>(() => ui.Inspect(snapshot.Snapshot, 0, oldHandle));
        ui.Act(snapshot.Snapshot, element.Reference, "click", null, null);
        Assert.Equal(1, clicks);
        Assert.Throws<UiAutomationException>(() => ui.Act(snapshot.Snapshot, element.Reference, "click", null, null));
        Assert.Throws<UiAutomationException>(() => ui.Inspect(null, 0, handle));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignWidgetContextOrDetachedMovieRootDisablesHitTests(bool movieRoot)
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        var probe = new HitCountingWidget(context) { Size = new Vector2(40, 40) };
        button.AddChild(probe);
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        if (movieRoot)
        {
            var upper = Context();
            var layer = screen.AddLayer(upper, true, true);
            Mock.Get(layer._movieIdentifiers[0].Movie).SetupGet(m => m.RootWidget).Returns(new Widget(upper));
        }
        else context.Root.AddChild(new Widget(Context()));
        Assert.False(adapter.Read(screen.Layer).ScopeComplete);
        Assert.Equal(0, probe.HitTests);
    }

    [Fact]
    public void DynamicUpperModalDescendantsInvalidateSelectedSnapshotEvenAwayFromButton()
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var upper = Context();
        var parent = new Widget(upper) { DoNotAcceptEvents = true, Size = new Vector2(800, 800) };
        var modal = new Widget(upper) { DoNotAcceptEvents = true, IsVisible = false, Size = new Vector2(40, 40), Left = 400 };
        upper.Root.DoNotAcceptEvents = true;
        parent.AddChild(modal);
        upper.Root.AddChild(parent);
        upper.Root.UpdatePosition();
        screen.AddLayer(upper, true, true);
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0, ui.Discover().Layers[0].Layer);
        var element = snapshot.Elements.Single(e => e.Widget.Native == button);
        modal.IsVisible = true;
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() =>
            ui.Act(snapshot.Snapshot, element.Reference, "click", null, null)).Code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SemanticActivationAllowsRootOrModalReplacementWithoutLaterBridgeTraversal(bool scoped, bool modal)
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var ui = new LiveTestUi(adapter);
        string handle = scoped ? ui.Discover().Layers[0].Layer : null;
        var snapshot = ui.Inspect(null, 0, handle);
        var element = snapshot.Elements.Single(e => e.Widget.Native == button);
        int clicks = 0;
        var notifications = new List<string>();
        button.EventFire += (_, name, _) => notifications.Add(name);
        button.boolPropertyChanged += (_, name, _) =>
        {
            if (name == "IsPressed") throw new InvalidOperationException("Semantic activation must not synthesize press state.");
        };
        context.EventManager.Time = 2;
        button.ClickEventHandlers.Add(_ =>
        {
            clicks++;
            if (modal)
            {
                var layer = new UnknownLayer();
                layer.IsActive = true;
                layer.InputRestrictions.SetInputRestrictions(true, TaleWorlds.Library.InputUsageMask.Mouse);
                ScreenManager.TopScreen._layers.Add(layer);
                ScreenManager._isSortedActiveLayersDirty = true;
            }
            else
            {
                var root = new Widget(context);
                root.AddChild(new Widget(context));
                root.Children[0] = null; // Any later bridge read or mouse release collection fails.
                context.EventManager.Root = root;
            }
        });
        button.ClickEventHandlers.Add(_ => clicks++);
        ui.Act(snapshot.Snapshot, element.Reference, "click", null, null);
        Assert.Equal(2, clicks);
        Assert.Equal(new[] { "Click" }, notifications);
        Assert.Equal(2, button._lastClickTime);
        Assert.False(button.IsPressed);
        Assert.False(context.EventManager._mouseIsDown);
        Assert.Null(context.EventManager.LatestMouseDownWidget);
        Assert.False(context._uiInputContext._isMousePositionOverridden);
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() =>
            ui.Act(snapshot.Snapshot, element.Reference, "click", null, null)).Code);
        if (scoped) Assert.Throws<UiAutomationException>(() => ui.Inspect(null, 0, handle));
    }

    [Fact]
    public void NativeClickEventCanFinalizeEventManagerBeforeNativeTimeContinuation()
    {
        var context = Context();
        var button = new ButtonWidget(context);
        context.EventManager.Time = 2;
        button.EventFire += (_, name, _) => { if (name == "Click") context.EventManager.OnFinalize(); };
        adapter.ActivateButton(button);
        Assert.Equal(2, button._lastClickTime);
        Assert.Null(context.EventManager._widgetContainers);
        Assert.Same(context, button.Context);
    }

    [Fact]
    public void SemanticActivationPreservesNativeClickToggleRadioAndDoubleClickOrdering()
    {
        var context = Context();
        var button = new ButtonWidget(context) { ButtonType = ButtonType.Toggle };
        var notifications = new List<string>();
        button.ClickEventHandlers.Add(_ => notifications.Add("handler:" + button.IsSelected));
        button.boolPropertyChanged += (_, name, _) => { if (name == "IsSelected") notifications.Add("selection"); };
        button.EventFire += (_, name, _) => notifications.Add(name);
        context.EventManager.Time = 2;
        adapter.ActivateButton(button);
        context.EventManager.Time = 2.1f;
        adapter.ActivateButton(button);
        Assert.Equal(new[] { "handler:False", "selection", "Click", "handler:True", "selection", "Click", "DoubleClick" }, notifications);
        var parent = new ListPanel(context);
        var radio = new ButtonWidget(context) { ButtonType = ButtonType.Radio };
        parent.AddChild(new ButtonWidget(context));
        parent.AddChild(radio);
        adapter.ActivateButton(radio);
        Assert.True(radio.IsSelected);
        Assert.Equal(1, parent.IntValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownButtonSubclassIsNotAdvertisedOrDispatched(bool scoped)
    {
        var context = Context();
        var button = new UnknownButton(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        Assert.Empty(adapter.Read(scoped ? screen.Layer : null).Widgets.Single(w => w.Native == button).Actions);
        Assert.Equal("unsupported_action", Assert.Throws<UiAutomationException>(() =>
            adapter.Act(new UiWidget { Native = button, Layer = screen.Layer }, "click", null, null, scoped)).Code);
        Assert.Equal("unsupported_action", Assert.Throws<UiAutomationException>(() => adapter.ActivateButton(button)).Code);
    }

    [Theory]
    [InlineData("mouse")]
    [InlineData("alternate")]
    [InlineData("drag")]
    [InlineData("pressed")]
    [InlineData("clickState")]
    public void SemanticActivationRejectsBusyInputWithoutCancellation(string busy)
    {
        var context = Context();
        var button = new ButtonWidget(context);
        int clicks = 0;
        button.ClickEventHandlers.Add(_ => clicks++);
        if (busy == "mouse") context.EventManager._mouseIsDown = true;
        if (busy == "alternate") context.EventManager._mouseAlternateIsDown = true;
        if (busy == "drag") context.EventManager.DraggedWidget = button;
        if (busy == "pressed") button.IsPressed = true;
        if (busy == "clickState") button._clickState = ButtonWidget.ButtonClickState.HandlingClick;
        Assert.Equal("input_busy", Assert.Throws<UiAutomationException>(() => adapter.ActivateButton(button)).Code);
        Assert.Equal(0, clicks);
        Assert.Equal(busy == "mouse", context.EventManager._mouseIsDown);
        Assert.Equal(busy == "alternate", context.EventManager._mouseAlternateIsDown);
        Assert.Equal(busy == "pressed", button.IsPressed);
        Assert.Equal(busy == "drag", context.EventManager.IsDragging);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeFailureAfterMutationIsUncertainAndConsumesSelectedReferences(bool bridgeShapedException)
    {
        var context = Context();
        var button = new ButtonWidget(context) { Size = new Vector2(40, 40) };
        context.Root.AddChild(button);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        var ui = new LiveTestUi(adapter);
        var handle = ui.Discover().Layers[0].Layer;
        var snapshot = ui.Inspect(null, 0, handle);
        var element = snapshot.Elements.Single(e => e.Widget.Native == button);
        button.ClickEventHandlers.Add(_ =>
        {
            button.Id = "changed";
            if (bridgeShapedException) throw new UiAutomationException("invalid_parameters", "native callback");
            button.ClickEventHandlers.Clear(); // Native foreach continuation throws after mutation.
        });
        var error = Assert.Throws<InvalidOperationException>(() => ui.Act(snapshot.Snapshot, element.Reference, "click", null, null));
        Assert.Contains("outcome is uncertain", error.Message);
        Assert.NotNull(error.InnerException);
        Assert.Equal("changed", button.Id);
        Assert.False(button.IsPressed);
        Assert.False(context.EventManager._mouseIsDown);
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() =>
            ui.Act(snapshot.Snapshot, element.Reference, "click", null, null)).Code);
        Assert.Throws<UiAutomationException>(() => ui.Inspect(null, 0, handle));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("slider")]
    [InlineData("scroll_vertical")]
    [InlineData("scroll_horizontal")]
    public void SelectedScopeDoesNotAdvertiseOrDispatchUnprovenNonButtonActions(string action)
    {
        var context = Context();
        Widget widget = action == "text" ? (Widget)new EditableTextWidget(context) :
            action == "slider" ? new SliderWidget(context) : new ScrollablePanel(context)
            {
                VerticalScrollbar = new ScrollbarWidget(context), HorizontalScrollbar = new ScrollbarWidget(context),
            };
        widget.Size = new Vector2(100, 100);
        context.Root.AddChild(widget);
        context.Root.UpdatePosition();
        using var screen = new TestScreenScope(context);
        Assert.Contains(action, adapter.Read().Widgets.Single(w => w.Native == widget).Actions);
        Assert.Empty(adapter.Read(screen.Layer).Widgets.Single(w => w.Native == widget).Actions);
        Assert.Equal("unsupported_action", Assert.Throws<UiAutomationException>(() =>
            adapter.Act(new UiWidget { Native = widget, Layer = screen.Layer }, action, "new", 0.5, scoped: true)).Code);
        Assert.Null(context.EventManager.FocusedWidget);
    }

    private sealed class UnknownButton : ButtonWidget
    {
        public UnknownButton(UIContext context) : base(context) { }
        public override void HandleClick() => throw new InvalidOperationException("Unknown native override must not run.");
    }

    private sealed class UnknownLayer : ScreenLayer
    {
        public UnknownLayer() : base("unknown", 100) { }
        public override bool HitTest(Vector2 point) => throw new InvalidOperationException("Must not call an unbounded unknown hit test.");
    }

    private sealed class HitCountingWidget : Widget
    {
        public int HitTests { get; private set; }
        public HitCountingWidget(UIContext context) : base(context) { }
        public override bool OnPreviewMousePressed()
        {
            HitTests++;
            return false;
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
            Layer.InputRestrictions = new InputRestrictions(0);
            System.Runtime.CompilerServices.Unsafe.AsRef(in Layer._movieIdentifiers) =
                new TaleWorlds.Library.MBList<GauntletMovieIdentifier>();
            var screen = new TestScreen();
            screen._layers.Add(Layer);
            ScreenManager.TopScreen = screen;
            ScreenManager._sortedLayers = new List<ScreenLayer> { Layer };
            ScreenManager._globalLayers = new ObservableCollection<GlobalLayer>();
        }
        public GauntletLayer AddLayer(UIContext context, bool above, bool blocks)
        {
            var layer = (GauntletLayer)FormatterServices.GetUninitializedObject(typeof(GauntletLayer));
            layer.UIContext = context;
            layer.IsActive = true;
            layer.InputRestrictions = new InputRestrictions(above ? 10 : -10);
            layer.InputRestrictions.SetInputRestrictions(true, blocks ? TaleWorlds.Library.InputUsageMask.Mouse : (TaleWorlds.Library.InputUsageMask)0);
            var movie = new Mock<TaleWorlds.GauntletUI.Data.IGauntletMovie>();
            movie.SetupGet(m => m.RootWidget).Returns(context.Root);
            var identifier = new GauntletMovieIdentifier("test", null) { Movie = movie.Object };
            System.Runtime.CompilerServices.Unsafe.AsRef(in layer._movieIdentifiers) =
                new TaleWorlds.Library.MBList<GauntletMovieIdentifier> { identifier };
            ScreenManager.TopScreen._layers.Add(layer);
            ScreenManager._isSortedActiveLayersDirty = true;
            return layer;
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
