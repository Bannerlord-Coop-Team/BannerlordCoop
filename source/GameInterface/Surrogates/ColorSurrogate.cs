using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ColorSurrogate
{
    [ProtoMember(1)]
    public float X { get; set; }

    [ProtoMember(2)]
    public float Y { get; set; }

    [ProtoMember(3)]
    public float Z { get; set; }

    [ProtoMember(4)]
    public float W { get; set; }

    public ColorSurrogate(Vec3 v)
    {
        X = v.x;
        Y = v.y;
        Z = v.z;
        W = v.w;
    }

    public static implicit operator ColorSurrogate(Vec3 v) => new ColorSurrogate(v);

    public static implicit operator Vec3(ColorSurrogate s) => new Vec3(s.X, s.Y, s.Z, s.W);
}
