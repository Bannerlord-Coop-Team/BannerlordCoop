using System;
using TaleWorlds.Library;

namespace Missions.Missiles.Message;

/// <summary>Packs a missile launch rotation into 64 bits as a smallest-three quaternion.</summary>
internal static class MissileOrientationCodec
{
    // Largest error allowed on any matrix element after a round trip. Vanilla multiplayer sends missile frames
    // as unit vectors with a 0.002 step, so this stays within what vanilla already shows.
    internal const float Tolerance = 0.001f;

    private const int ComponentBits = 20;
    private const int IndexShift = 3 * ComponentBits;
    private const ulong ComponentMask = (1UL << ComponentBits) - 1;
    // Every component but the largest of a unit quaternion lies within +-1/sqrt(2).
    private const float ComponentRange = 0.70710677f;
    private const float Scale = ComponentMask / (2f * ComponentRange);

    /// <summary>Packs <paramref name="orientation"/>, failing when the unpacked frame would be off by more than <see cref="Tolerance"/>.</summary>
    public static bool TryPack(Mat3 orientation, out ulong packed)
    {
        Quaternion quat = Quaternion.QuaternionFromMat3(orientation);
        float[] components = { quat.X, quat.Y, quat.Z, quat.W };

        int largest = 0;
        for (int i = 1; i < components.Length; i++)
        {
            if (Math.Abs(components[i]) > Math.Abs(components[largest]))
                largest = i;
        }

        // q and -q are the same rotation, so a positive largest component lets the receiver rebuild it.
        float sign = components[largest] < 0f ? -1f : 1f;
        packed = (ulong)largest << IndexShift;
        int shift = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (i == largest) continue;

            packed |= Quantize(components[i] * sign) << shift;
            shift += ComponentBits;
        }

        return IsWithinTolerance(orientation, Unpack(packed));
    }

    public static Mat3 Unpack(ulong packed)
    {
        int largest = (int)(packed >> IndexShift) & 3;
        float[] components = new float[4];
        float sumOfSquares = 0f;
        int shift = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (i == largest) continue;

            components[i] = Dequantize((packed >> shift) & ComponentMask);
            sumOfSquares += components[i] * components[i];
            shift += ComponentBits;
        }

        components[largest] = (float)Math.Sqrt(Math.Max(0f, 1f - sumOfSquares));
        return new Quaternion(components[0], components[1], components[2], components[3]).ToMat3();
    }

    private static ulong Quantize(float component)
    {
        float scaled = (float)Math.Round((component + ComponentRange) * Scale);
        if (scaled <= 0f) return 0;
        return scaled >= ComponentMask ? ComponentMask : (ulong)scaled;
    }

    private static float Dequantize(ulong value) => (value / Scale) - ComponentRange;

    // A NaN element compares false, so a NaN frame fails too.
    private static bool IsWithinTolerance(Mat3 expected, Mat3 actual) =>
        IsWithinTolerance(expected.s, actual.s) &&
        IsWithinTolerance(expected.f, actual.f) &&
        IsWithinTolerance(expected.u, actual.u);

    private static bool IsWithinTolerance(Vec3 expected, Vec3 actual) =>
        Math.Abs(expected.x - actual.x) <= Tolerance &&
        Math.Abs(expected.y - actual.y) <= Tolerance &&
        Math.Abs(expected.z - actual.z) <= Tolerance;
}
