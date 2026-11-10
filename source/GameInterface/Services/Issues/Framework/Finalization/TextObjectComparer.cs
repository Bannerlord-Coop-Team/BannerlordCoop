using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Matches journal rows by text for both live mirroring and ending replay.</summary>
internal class TextObjectComparer : ITextObjectComparer
{
    public bool SameText(TextObject first, TextObject second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first == null || second == null || first.Value != second.Value ||
            (first.Attributes == null) != (second.Attributes == null) ||
            (first.Attributes?.Count ?? 0) != (second.Attributes?.Count ?? 0)) return false;
        if (first.Attributes == null) return true;
        foreach (var pair in first.Attributes)
        {
            if (second.Attributes == null || !second.Attributes.TryGetValue(pair.Key, out var value)) return false;
            if (pair.Value is TextObject text)
            {
                if (!(value is TextObject other) || !SameText(text, other)) return false;
            }
            else if (!Equals(pair.Value, value)) return false;
        }
        return true;
    }

    public bool SameKey(TextObject first, TextObject second) =>
        ReferenceEquals(first, second) || (first != null && second != null && first.Value == second.Value);
}
