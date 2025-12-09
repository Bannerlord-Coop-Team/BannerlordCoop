using ProtoBuf;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct MBGUIDSurrogate
{
    [ProtoMember(1)]
    public uint InternalValue { get; set; }

    public MBGUIDSurrogate(MBGUID guid)
    {
        InternalValue = guid.InternalValue;
    }

    public static implicit operator MBGUIDSurrogate(MBGUID guid)
    {
        return new MBGUIDSurrogate(guid);
    }

    public static implicit operator MBGUID(MBGUIDSurrogate surrogate)
    {
        return new MBGUID(surrogate.InternalValue);
    }
}
