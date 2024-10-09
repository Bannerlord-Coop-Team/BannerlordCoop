using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct Vec2Surrogate
{
    [ProtoMember(1)]
    public float X { get; set; }

    [ProtoMember(2)]
    public float Y { get; set; }

    public Vec2Surrogate(Vec2 vec2)
    {
        X = vec2.X;
        Y = vec2.Y;
    }

    public static implicit operator Vec2Surrogate(Vec2 vec2)
    {
        return new Vec2Surrogate(vec2);
    }

    public static implicit operator Vec2(Vec2Surrogate surrogate)
    {
        return new Vec2(surrogate.X, surrogate.Y);
    }
}
