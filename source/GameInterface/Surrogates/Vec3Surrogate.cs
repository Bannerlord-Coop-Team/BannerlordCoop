using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct Vec3Surrogate
{
    [ProtoMember(1)]
    public float X { get; set; }

    [ProtoMember(3)]
    public float Y { get; set; }
    [ProtoMember(4)]
    public float Z { get; set; }
    public Vec3Surrogate(Vec3 vec3)
    {
        X = vec3.X;
        Y = vec3.Y;
        Z = vec3.Z;
    }

    public static implicit operator Vec3Surrogate(Vec3 vec3)
    {
        return new Vec3Surrogate(vec3);
    }

    public static implicit operator Vec3(Vec3Surrogate surrogate)
    {
        return new Vec3(surrogate.X, surrogate.Y, surrogate.Z);
    }
}
