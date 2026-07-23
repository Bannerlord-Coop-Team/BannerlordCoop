using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct TextObjectSurrogate
{
    [ProtoMember(1)]
    public string Text { get; set; }

    // TextObjects can have saved attributes used in producing the actual text
    // These need to be sent over the network to have the same name
    // An example of this is PartyBase.CustomName, which uses a CLAN_NAME TextObject attribute
    [ProtoMember(2)]
    public Dictionary<string, TextObjectSurrogate> Attributes { get; set; }

    public TextObjectSurrogate(TextObject textObject)
    {
        Text = textObject?.Value;
        Attributes = null;

        if (textObject?.Attributes == null)
            return;

        Attributes = new();
        foreach (var attribute in textObject.Attributes)
        {
            if (attribute.Value is TextObject textVariable)
            {
                Attributes[attribute.Key] = new TextObjectSurrogate(textVariable);
            }
        }
    }

    public static implicit operator TextObjectSurrogate(TextObject textObject)
    {
        return new TextObjectSurrogate(textObject);
    }

    public static implicit operator TextObject(TextObjectSurrogate surrogate)
    {
        if (surrogate.Attributes == null)
            return new TextObject(surrogate.Text);

        var attributes = new Dictionary<string, object>();
        foreach (var attribute in surrogate.Attributes)
        {
            TextObject converted = attribute.Value;
            attributes[attribute.Key] = converted;
        }

        return new TextObject(surrogate.Text, attributes);
    }
}