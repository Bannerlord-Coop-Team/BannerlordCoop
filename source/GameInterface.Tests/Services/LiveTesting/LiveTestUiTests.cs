#if DEBUG
using System;
using System.Linq;
using System.Text.Json;
using GameInterface.Services.LiveTesting;
using Xunit;

namespace GameInterface.Tests.Services.LiveTesting;

public class LiveTestUiTests
{
    private readonly FakeAdapter adapter = new FakeAdapter();

    [Fact]
    public void InspectPagesBoundedTreeWithoutSerializingNativeReferences()
    {
        for (int i = 1; i < 300; i++) adapter.Frame.Widgets.Add(adapter.Node());
        var ui = new LiveTestUi(adapter);
        var first = ui.Inspect(null, 0);
        Assert.Equal(128, first.Elements.Count);
        var second = ui.Inspect(first.Snapshot, first.NextOffset);
        Assert.Equal("e128", second.Elements[0].Reference);
        Assert.Equal(256, second.NextOffset);
        var last = ui.Inspect(first.Snapshot, second.NextOffset);
        Assert.Equal(44, last.Elements.Count);
        Assert.Equal(300, last.NextOffset);
        Assert.DoesNotContain("native", JsonSerializer.Serialize(first).ToLowerInvariant());
    }

    [Theory]
    [InlineData("click")]
    [InlineData("toggle")]
    [InlineData("text")]
    [InlineData("slider")]
    [InlineData("scroll_vertical")]
    [InlineData("scroll_horizontal")]
    public void ActionsUseNativeAdapterOnceThenConsumeReferences(string action)
    {
        adapter.Frame.Widgets[0].Actions = new[] { action };
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0).Snapshot;
        ui.Act(snapshot, "e0", action, "hello", 1);
        Assert.Equal(1, adapter.Actions);
        Assert.Equal(action, adapter.LastAction);
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() => ui.Act(snapshot, "e0", action, null, 1)).Code);
    }

    [Fact]
    public void NativeFailureCannotBeRetriedWithConsumedReference()
    {
        adapter.Throw = true;
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0).Snapshot;
        Assert.Throws<InvalidOperationException>(() => ui.Act(snapshot, "e0", "click", null, null));
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() => ui.Act(snapshot, "e0", "click", null, null)).Code);
        Assert.Equal(1, adapter.Actions);
    }

    [Theory]
    [InlineData("screen")]
    [InlineData("identity")]
    [InlineData("parent")]
    [InlineData("layer")]
    [InlineData("id")]
    [InlineData("label")]
    [InlineData("value")]
    [InlineData("bounds")]
    [InlineData("range")]
    [InlineData("newchild")]
    public void ChangedScreenTreeOrTargetFailsBeforeDispatch(string change)
    {
        var ui = new LiveTestUi(adapter);
        string snapshot = ui.Inspect(null, 0).Snapshot;
        adapter.Copy();
        var node = adapter.Frame.Widgets[0];
        switch (change)
        {
            case "screen": adapter.Frame.Screen = new object(); break;
            case "identity": node.Native = new object(); break;
            case "layer": node.Layer = new object(); break;
            case "parent": node.Parent = 5; break;
            case "id": node.Id = "other"; break;
            case "label": node.Text = "other"; break;
            case "value": node.Value = "other"; break;
            case "bounds": node.X = 12; break;
            case "range": node.Maximum = 5; break;
            case "newchild": adapter.Frame.Widgets.Add(adapter.Node()); break;
        }
        Assert.Equal("stale_reference", Assert.Throws<UiAutomationException>(() => ui.Act(snapshot, "e0", "click", null, null)).Code);
        Assert.Equal(0, adapter.Actions);
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("covered")]
    [InlineData("password")]
    [InlineData("truncated")]
    public void UnsafeTargetsFailClosed(string kind)
    {
        var node = adapter.Frame.Widgets[0];
        if (kind == "hidden") node.Visible = false;
        if (kind == "disabled") node.Enabled = false;
        if (kind == "covered") node.HitTestable = false;
        if (kind == "password") node.Redacted = true;
        if (kind == "truncated") adapter.Frame.Truncated = true;
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0).Snapshot;
        Assert.Throws<UiAutomationException>(() => ui.Act(snapshot, "e0", "click", null, null));
        Assert.Equal(0, adapter.Actions);
    }

    [Fact]
    public void BenignSubpixelLayoutAndUnrelatedTextDoNotInvalidateTarget()
    {
        adapter.Frame.Widgets.Add(adapter.Node());
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0).Snapshot;
        adapter.Copy();
        adapter.Frame.Widgets[0].X = 0.5f;
        adapter.Frame.Widgets[1].Text = "clock tick";
        ui.Act(snapshot, "e0", "click", null, null);
        Assert.Equal(1, adapter.Actions);
    }

    [Fact]
    public void LargeUntruncatedTreeCanPageAndDispatchBeyond4096()
    {
        for (int i = 1; i < 5000; i++) adapter.Frame.Widgets.Add(adapter.Node());
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0).Snapshot;
        var page = ui.Inspect(snapshot, 4096);
        Assert.Equal(128, page.Elements.Count);
        Assert.Equal("e4096", page.Elements[0].Reference);
        ui.Act(snapshot, "e4096", "click", null, null);
        Assert.Equal(1, adapter.Actions);
    }

    [Fact]
    public void NewInspectionInvalidatesPreviousSnapshot()
    {
        var ui = new LiveTestUi(adapter);
        var previous = ui.Inspect(null, 0).Snapshot;
        ui.Inspect(null, 0);
        Assert.Throws<UiAutomationException>(() => ui.Act(previous, "e0", "click", null, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16385)]
    [InlineData(1)]
    public void InvalidInitialPagesFail(int offset) =>
        Assert.Throws<UiAutomationException>(() => new LiveTestUi(adapter).Inspect(null, offset));

    [Theory]
    [InlineData("invoke", null, null)]
    [InlineData("click", null, double.NaN)]
    [InlineData("click", null, double.PositiveInfinity)]
    public void UnsupportedActionsAndNonfiniteValuesDoNotDispatch(string action, string text, double? value)
    {
        var ui = new LiveTestUi(adapter);
        var snapshot = ui.Inspect(null, 0).Snapshot;
        Assert.Throws<UiAutomationException>(() => ui.Act(snapshot, "e0", action, text, value));
        Assert.Equal(0, adapter.Actions);
    }

    private sealed class FakeAdapter : IUiWidgetAdapter
    {
        public UiFrame Frame = new UiFrame { Screen = new object(), ScreenName = "menu" };
        public int Actions;
        public bool Throw;
        public string LastAction;
        public FakeAdapter() { Frame.Widgets.Add(Node()); }
        public UiWidget Node() => new UiWidget
        {
            Native = new object(), Layer = new object(), Parent = -1, Id = "button", Type = "ButtonWidget",
            Text = "Apply", Visible = true, Enabled = true, HitTestable = true, Width = 20, Height = 20,
            Actions = new[] { "click" },
        };
        public UiFrame Read() => Frame;
        public void Act(UiWidget target, string action, string text, double? value)
        {
            Actions++; LastAction = action;
            if (Throw) throw new InvalidOperationException("after native side effect");
        }
        public void Copy()
        {
            var clone = new UiFrame { Screen = Frame.Screen, ScreenName = Frame.ScreenName, Truncated = Frame.Truncated };
            foreach (var n in Frame.Widgets)
            {
                var c = JsonSerializer.Deserialize<UiWidget>(JsonSerializer.Serialize(n));
                c.Native = n.Native; c.Layer = n.Layer;
                clone.Widgets.Add(c);
            }
            Frame = clone;
        }
    }
}
#endif
