using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;
internal struct TextObjectSurrogate
{
    [ProtoMember(1)]
    public string Value { get; }

    public TextObjectSurrogate(TextObject textObject)
    {
        Value = textObject.Value;
    }

    public static implicit operator TextObjectSurrogate(TextObject textObject)
    {
        return new TextObjectSurrogate(textObject);
    }

    public static implicit operator TextObject(TextObjectSurrogate surrogate)
    {
        return new TextObject(surrogate.Value);
    }
}
