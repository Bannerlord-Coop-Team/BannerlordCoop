using TaleWorlds.Library;

namespace GameInterface.Services.UI;

/// <summary>
/// Maps each player to a stable, visually distinct color for UI that needs to tell ally players apart
/// (e.g. the kill feed). The mapping is a deterministic hash of the player's controller id, not session
/// state, so the same controller id always gets the same color — across battles, reconnects, and restarts.
/// </summary>
public static class PlayerColorAssigner
{
    // Categorical palette tuned to stay clear of the vanilla kill-feed team colors (friendly green
    // ~(0.54,0.78,0.42), enemy red ~(0.95,0.49,0.43)) so a per-player color is never confused with them.
    private static readonly Color[] Palette =
    {
        new Color(0.231f, 0.510f, 0.965f), // blue
        new Color(0.545f, 0.361f, 0.965f), // violet
        new Color(0.961f, 0.620f, 0.043f), // amber
        new Color(0.024f, 0.714f, 0.831f), // cyan
        new Color(0.980f, 0.800f, 0.082f), // yellow
        new Color(0.388f, 0.400f, 0.945f), // indigo
        new Color(0.078f, 0.722f, 0.651f), // teal
        new Color(0.851f, 0.275f, 0.937f), // fuchsia
        new Color(0.055f, 0.647f, 0.914f), // sky blue
        new Color(0.659f, 0.333f, 0.969f), // purple
        new Color(0.918f, 0.702f, 0.031f), // gold
        new Color(0.220f, 0.741f, 0.973f), // light blue
        new Color(0.753f, 0.518f, 0.988f), // light purple
        new Color(0.988f, 0.827f, 0.302f), // light yellow
    };

    private static readonly Color FallbackColor = Palette[0];

    public static Color GetColor(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return FallbackColor;

        return Palette[HashToIndex(controllerId)];
    }

    // string.GetHashCode() is randomized per process in .NET, so it can't be used here.
    private static int HashToIndex(string controllerId)
    {
        var hash = 2166136261u;
        foreach (var c in controllerId)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return (int)(hash % (uint)Palette.Length);
    }
}
