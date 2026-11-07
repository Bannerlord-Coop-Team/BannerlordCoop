using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface ITextObjectComparer
{
    // Same raw value and the same attributes, nested text included; null attributes differ from empty ones
    bool SameText(TextObject first, TextObject second);

    // Same raw value only, so a row whose variables changed still matches
    bool SameKey(TextObject first, TextObject second);
}
