using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct Vec3Surrogate
{
    [ProtoMember(1, IsPacked = true)] public float[] V;

    public static implicit operator Vec3Surrogate(Vec3 v)
        => new Vec3Surrogate { V = new[] { v.X, v.Y, v.Z } };
    public static implicit operator Vec3(Vec3Surrogate s)
        => new Vec3(s.V[0], s.V[1], s.V[2]);
}
