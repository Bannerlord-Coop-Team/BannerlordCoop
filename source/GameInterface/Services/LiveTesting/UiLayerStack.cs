#if DEBUG
using System;
using System.Collections.Generic;

namespace GameInterface.Services.LiveTesting;

/// <summary>Bounded layer identities and input restrictions, without traversing widget descendants.</summary>
public sealed class UiLayerStack
{
    internal object Screen;
    internal object Focus;
    internal readonly List<UiLayerState> Layers = new List<UiLayerState>();
    public string ScreenName { get; set; }
    public bool Truncated { get; set; }

    internal bool Matches(UiLayerStack other)
    {
        if (other == null || Truncated || other.Truncated || !ReferenceEquals(Screen, other.Screen) ||
            !ReferenceEquals(Focus, other.Focus) || Layers.Count != other.Layers.Count) return false;
        for (int i = 0; i < Layers.Count; i++)
            if (!Layers[i].Matches(other.Layers[i])) return false;
        return true;
    }
}

internal sealed class UiLayerState
{
    internal object Native;
    internal object Context;
    internal object Root;
    internal bool Active;
    internal bool Finalized;
    internal bool FocusLayer;
    internal int InputMask;
    internal int Order;
    internal int RootCount;
    internal bool RootVisible;
    internal bool RootEnabled;
    internal string Name;
    internal string Type;
    internal bool Supported;
    internal readonly List<object> Roots = new List<object>();
    internal readonly List<object> Movies = new List<object>();
    internal readonly List<bool> RootStates = new List<bool>();

    internal bool Matches(UiLayerState other)
    {
        if (!ReferenceEquals(Native, other.Native) || !ReferenceEquals(Context, other.Context) ||
            !ReferenceEquals(Root, other.Root) || Active != other.Active || Finalized != other.Finalized ||
            FocusLayer != other.FocusLayer || InputMask != other.InputMask || Order != other.Order || RootCount != other.RootCount ||
            RootVisible != other.RootVisible || RootEnabled != other.RootEnabled ||
            Roots.Count != other.Roots.Count || Movies.Count != other.Movies.Count || RootStates.Count != other.RootStates.Count) return false;
        for (int i = 0; i < Roots.Count; i++)
            if (!ReferenceEquals(Roots[i], other.Roots[i])) return false;
        for (int i = 0; i < RootStates.Count; i++)
            if (RootStates[i] != other.RootStates[i]) return false;
        for (int i = 0; i < Movies.Count; i++)
            if (!ReferenceEquals(Movies[i], other.Movies[i])) return false;
        return true;
    }
}

/// <summary>Opaque handles are issued only for a complete bounded layer stack.</summary>
public sealed class UiLayerSnapshot
{
    public string Screen { get; set; }
    public bool Truncated { get; set; }
    public int MaximumLayers { get; set; }
    public List<UiLayerDescription> Layers { get; } = new List<UiLayerDescription>();
}

public sealed class UiLayerDescription
{
    public string Layer { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public int StackIndex { get; set; }
    public bool Active { get; set; }
    public bool Visible { get; set; }
    public int RootCount { get; set; }
    public bool Selectable { get; set; }
    public int InputMask { get; set; }
}
#endif
