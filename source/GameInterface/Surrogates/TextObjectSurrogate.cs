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
    public Dictionary<string, TextObjectSurrogate> TextObjectAttributes { get; set; }

    [ProtoMember(3)]
    public Dictionary<string, int> IntAttributes { get; set; }

    [ProtoMember(4)]
    public Dictionary<string, string> StringAttributes { get; set; }

    public TextObjectSurrogate(TextObject textObject)
    {
        Text = textObject?.Value;
        TextObjectAttributes = new();
        IntAttributes = new();
        StringAttributes = new();

        if (textObject?.Attributes == null)
            return;

        foreach (var attribute in textObject.Attributes)
        {
            if (attribute.Value is TextObject textVariable)
            {
                TextObjectAttributes[attribute.Key] = new TextObjectSurrogate(textVariable);
            }
            else if (attribute.Value is int integerVariable)
            {
                IntAttributes[attribute.Key] = integerVariable;
            }
            else if (attribute.Value is string stringVariable)
            {
                StringAttributes[attribute.Key] = stringVariable;
            }
        }
    }

    public static implicit operator TextObjectSurrogate(TextObject textObject)
    {
        return new TextObjectSurrogate(textObject);
    }

    public static implicit operator TextObject(TextObjectSurrogate surrogate)
    {
        // Keep null attributes instead of initializing an empty dictionary
        if (surrogate.TextObjectAttributes == null 
            && surrogate.IntAttributes == null
            && surrogate.StringAttributes == null)
            return new TextObject(surrogate.Text);

        var attributes = new Dictionary<string, object>();
        if (surrogate.TextObjectAttributes != null)
        {
            foreach (var textObjectAttribute in surrogate.TextObjectAttributes)
            {
                TextObject converted = textObjectAttribute.Value;
                attributes[textObjectAttribute.Key] = converted;
            }
        }
        if (surrogate.IntAttributes != null)
        {
            foreach (var integerAttribute in surrogate.IntAttributes)
            {
                attributes[integerAttribute.Key] = integerAttribute.Value;
            }
        }
        if (surrogate.StringAttributes != null)
        {
            foreach (var stringAttribute in surrogate.StringAttributes)
            {
                attributes[stringAttribute.Key] = stringAttribute.Value;
            }
        }

        return new TextObject(surrogate.Text, attributes);
    }
}