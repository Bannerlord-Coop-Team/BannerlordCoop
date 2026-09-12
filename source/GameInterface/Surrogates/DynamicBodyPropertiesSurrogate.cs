using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct DynamicBodyPropertiesSurrogate
{
    [ProtoMember(1)]
    public float Age { get; set; }

    [ProtoMember(2)]
    public float Weight { get; set; }

    [ProtoMember(3)]
    public float Build { get; set; }

    public DynamicBodyPropertiesSurrogate(DynamicBodyProperties dynamicBodyProperties)
    {
        Age = dynamicBodyProperties.Age;
        Weight = dynamicBodyProperties.Weight;
        Build = dynamicBodyProperties.Build;
    }

    public static implicit operator DynamicBodyPropertiesSurrogate(DynamicBodyProperties dynamicBodyProperties)
    {
        return new DynamicBodyPropertiesSurrogate(dynamicBodyProperties);
    }

    public static implicit operator DynamicBodyProperties(DynamicBodyPropertiesSurrogate surrogate)
    {
        return new DynamicBodyProperties(surrogate.Age, surrogate.Weight, surrogate.Build);
    }
}

