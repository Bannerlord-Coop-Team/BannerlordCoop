using ProtoBuf;
using System.Runtime.InteropServices;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct Vec3Surrogate
{
    [ProtoMember(1, DataFormat = DataFormat.FixedSize)]
    public ulong XY { get; set; }

    [ProtoMember(2)]
    public float Z { get; set; }

    public Vec3Surrogate(Vec3 v)
    {
        XY = GetBits(v.x) | ((ulong)GetBits(v.y) << 32);
        Z = v.z;
    }

    public static implicit operator Vec3Surrogate(Vec3 v) => new Vec3Surrogate(v);

    public static implicit operator Vec3(Vec3Surrogate s) => new Vec3(
        GetFloat((uint)s.XY),
        GetFloat((uint)(s.XY >> 32)),
        s.Z);

    private static uint GetBits(float value) => new FloatBits { Float = value }.Bits;

    private static float GetFloat(uint bits) => new FloatBits { Bits = bits }.Float;

    [StructLayout(LayoutKind.Explicit)]
    private struct FloatBits
    {
        [FieldOffset(0)]
        public float Float;

        [FieldOffset(0)]
        public uint Bits;
    }
}
