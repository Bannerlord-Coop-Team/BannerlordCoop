using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct TextObjectSurrogate
{
    [ProtoMember(1)]
    public string Text { get; set; }

    public TextObjectSurrogate(TextObject textObject)
    {
        Text = textObject?.Value;
    }

    public static implicit operator TextObjectSurrogate(TextObject textObject)
    {
        return new TextObjectSurrogate(textObject);
    }

    public static implicit operator TextObject(TextObjectSurrogate surrogate)
    {
        return new TextObject(surrogate.Text);
    }
}