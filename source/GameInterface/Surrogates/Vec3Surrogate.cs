using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct Vec3Surrogate
{
    [ProtoMember(1)]
    public float X { get; set; }

    [ProtoMember(2)]
    public float Y { get; set; }
    [ProtoMember(3)]
    public float Z { get; set; }
    [ProtoMember(4)]
    public float W { get; set; }

    public Vec3Surrogate(Vec3 vec3)
    {
        X = vec3.x;
        Y = vec3.y;
        Z = vec3.z;
        W = vec3.w;
    }

    public static implicit operator Vec3Surrogate(Vec3 vec3)
    {
        return new Vec3Surrogate(vec3);
    }

    public static implicit operator Vec3(Vec3Surrogate surrogate)
    {
        return new Vec3(surrogate.X, surrogate.Y, surrogate.Z, surrogate.W);
    }
}
