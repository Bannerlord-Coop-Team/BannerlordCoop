using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

internal static class JournalTextComparer
{
    public static bool Same(TextObject first, TextObject second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first == null || second == null || first.Value != second.Value ||
            (first.Attributes?.Count ?? 0) != (second.Attributes?.Count ?? 0)) return false;
        if (first.Attributes == null) return true;
        foreach (var pair in first.Attributes)
        {
            if (second.Attributes == null || !second.Attributes.TryGetValue(pair.Key, out var value)) return false;
            if (pair.Value is TextObject text)
            {
                if (!(value is TextObject other) || !Same(text, other)) return false;
            }
            else if (!Equals(pair.Value, value)) return false;
        }
        return true;
    }
}
