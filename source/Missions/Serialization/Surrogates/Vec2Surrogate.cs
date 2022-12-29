using ProtoBuf;
using TaleWorlds.Library;

namespace Missions.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct Vec2Surrogate
    {
        [ProtoMember(1)]
        public float X { get; }
        [ProtoMember(2)]
        public float Y { get; }

        public Vec2Surrogate(Vec2 obj)
        {
            X = obj.X;
            Y = obj.Y;
        }

        private Vec2 Deserialize()
        {
            return new Vec2(X, Y);
        }

        public static implicit operator Vec2Surrogate(Vec2 obj)
        {
            return new Vec2Surrogate(obj);
        }

        public static implicit operator Vec2(Vec2Surrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
