using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct BodyPropertiesSurrogate
{
    [ProtoMember(1)]
    public DynamicBodyProperties DynamicBodyProperties { get; set; }

    [ProtoMember(2)]
    public StaticBodyProperties StaticBodyProperties { get; set; }

    public BodyPropertiesSurrogate(BodyProperties bodyProperties)
    {
        DynamicBodyProperties = bodyProperties.DynamicProperties;
        StaticBodyProperties = bodyProperties.StaticProperties;
    }

    public static implicit operator BodyPropertiesSurrogate(BodyProperties bodyProperties)
    {
        return new BodyPropertiesSurrogate(bodyProperties);
    }

    public static implicit operator BodyProperties(BodyPropertiesSurrogate surrogate)
    {
        return new BodyProperties(surrogate.DynamicBodyProperties, surrogate.StaticBodyProperties);
    }
}

