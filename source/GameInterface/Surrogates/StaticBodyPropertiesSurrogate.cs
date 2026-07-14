using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct StaticBodyPropertiesSurrogate
{
    [ProtoMember(1)]
    public ulong KeyPart1 { get; set; }

    [ProtoMember(2)]
    public ulong KeyPart2 { get; set; }

    [ProtoMember(3)]
    public ulong KeyPart3 { get; set; }

    [ProtoMember(4)]
    public ulong KeyPart4 { get; set; }

    [ProtoMember(5)]
    public ulong KeyPart5 { get; set; }

    [ProtoMember(6)]
    public ulong KeyPart6 { get; set; }

    [ProtoMember(7)]
    public ulong KeyPart7 { get; set; }

    [ProtoMember(8)]
    public ulong KeyPart8 { get; set; }

    public StaticBodyPropertiesSurrogate(StaticBodyProperties staticBodyProperties)
    {
        KeyPart1 = staticBodyProperties.KeyPart1;
        KeyPart2 = staticBodyProperties.KeyPart2;
        KeyPart3 = staticBodyProperties.KeyPart3;
        KeyPart4 = staticBodyProperties.KeyPart4;
        KeyPart5 = staticBodyProperties.KeyPart5;
        KeyPart6 = staticBodyProperties.KeyPart6;
        KeyPart7 = staticBodyProperties.KeyPart7;
        KeyPart8 = staticBodyProperties.KeyPart8;
    }

    public static implicit operator StaticBodyPropertiesSurrogate(StaticBodyProperties staticBodyProperties)
    {
        return new StaticBodyPropertiesSurrogate(staticBodyProperties);
    }

    public static implicit operator StaticBodyProperties(StaticBodyPropertiesSurrogate surrogate)
    {
        return new StaticBodyProperties(
            surrogate.KeyPart1,
            surrogate.KeyPart2,
            surrogate.KeyPart3,
            surrogate.KeyPart4,
            surrogate.KeyPart5,
            surrogate.KeyPart6,
            surrogate.KeyPart7,
            surrogate.KeyPart8
        );
    }
}

