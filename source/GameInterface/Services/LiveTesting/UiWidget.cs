#if DEBUG
using System;
using System.Collections.Generic;

namespace GameInterface.Services.LiveTesting;

/// <summary>A bounded, game-thread observation; native identities never cross the pipe.</summary>
public sealed class UiFrame
{
    public object Screen { get; set; }
    public string ScreenName { get; set; }
    public List<UiWidget> Widgets { get; } = new List<UiWidget>();
    public bool Truncated { get; set; }
    public bool ScopeComplete { get; set; } = true;
    public int DomainWidgets { get; set; }
    internal UiLayerStack Stack;
    internal List<UiWidget> OcclusionWidgets = new List<UiWidget>();
}

/// <summary>Serializable widget description with a separate in-process native identity.</summary>
public sealed class UiWidget
{
    internal object Native;
    internal object Layer;
    internal bool ContentTruncated;
    public int Parent { get; set; }
    public int ChildIndex { get; set; }
    public string Id { get; set; }
    public string Type { get; set; }
    public string Text { get; set; }
    public string Value { get; set; }
    public bool Redacted { get; set; }
    public bool Visible { get; set; }
    public bool Enabled { get; set; }
    public bool HitTestable { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float? Minimum { get; set; }
    public float? Maximum { get; set; }
    public float? Step { get; set; }
    public string[] Actions { get; set; } = Array.Empty<string>();
}

/// <summary>A predictable rejection before dispatch, distinct from uncertain native failures.</summary>
public sealed class UiAutomationException : Exception
{
    public string Code { get; }
    public UiAutomationException(string code, string message) : base(message) { Code = code; }
}
#endif
