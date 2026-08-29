using GameInterface.Surrogates;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class Vec3SurrogateTests
{
    public static IEnumerable<object[]> ExactBitCases()
    {
        yield return new object[] { 1f, 2f, 3f };
        yield return new object[] { -123.5f, 0.125f, -7.25f };
        yield return new object[] { float.MaxValue, float.MinValue, float.Epsilon };
        yield return new object[] { BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)), 0f, 1f };
        yield return new object[]
        {
            BitConverter.Int32BitsToSingle(unchecked((int)0x7FC12345)),
            float.PositiveInfinity,
            float.NegativeInfinity,
        };
    }

    [Theory]
    [MemberData(nameof(ExactBitCases))]
    public void Serialize_RoundTripPreservesExactComponents(float x, float y, float z)
    {
        Vec3Surrogate surrogate = new Vec3(x, y, z);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, surrogate);
        stream.Position = 0;
        Vec3 roundTripped = Serializer.Deserialize<Vec3Surrogate>(stream);

        Assert.Equal(BitConverter.SingleToInt32Bits(x), BitConverter.SingleToInt32Bits(roundTripped.x));
        Assert.Equal(BitConverter.SingleToInt32Bits(y), BitConverter.SingleToInt32Bits(roundTripped.y));
        Assert.Equal(BitConverter.SingleToInt32Bits(z), BitConverter.SingleToInt32Bits(roundTripped.z));
    }

    [Theory]
    [InlineData(1f, 2f, 3f, 14)]
    [InlineData(1f, 2f, 0f, 9)]
    [InlineData(0f, 0f, 0f, 0)]
    public void Serialize_UsesCompactWireSize(float x, float y, float z, int expectedBytes)
    {
        Vec3Surrogate surrogate = new Vec3(x, y, z);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, surrogate);

        Assert.Equal((long)expectedBytes, stream.Length);
    }

    [Fact]
    public void ImplicitConversions_AllocateNoMemoryAfterWarmup()
    {
        var value = new Vec3(1024.25f, -2048.5f, 512.75f);
        float sum = ConvertRepeatedly(value, 1_000);

        long before = GC.GetAllocatedBytesForCurrentThread();
        sum += ConvertRepeatedly(value, 1_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(sum);

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ContainingTypes_RoundTripPackedVec3Values()
    {
        _ = new SurrogateCollection();
        var matrix = new Mat3(
            new Vec3(-123.5f, 0.125f, float.MaxValue),
            new Vec3(float.PositiveInfinity, -0f, 3.25f),
            new Vec3(float.NegativeInfinity, float.Epsilon, -7.5f));
        var frame = new MatrixFrame(matrix, new Vec3(1024.25f, -2048.5f, 512.75f));

        Mat3 roundTrippedMatrix = Serializer.DeepClone(matrix);
        MatrixFrame roundTrippedFrame = Serializer.DeepClone(frame);

        AssertVec3BitsEqual(matrix.f, roundTrippedMatrix.f);
        AssertVec3BitsEqual(matrix.s, roundTrippedMatrix.s);
        AssertVec3BitsEqual(matrix.u, roundTrippedMatrix.u);
        AssertVec3BitsEqual(frame.rotation.f, roundTrippedFrame.rotation.f);
        AssertVec3BitsEqual(frame.rotation.s, roundTrippedFrame.rotation.s);
        AssertVec3BitsEqual(frame.rotation.u, roundTrippedFrame.rotation.u);
        AssertVec3BitsEqual(frame.origin, roundTrippedFrame.origin);
    }

    private static float ConvertRepeatedly(Vec3 value, int count)
    {
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            Vec3Surrogate surrogate = value;
            Vec3 roundTripped = surrogate;
            sum += roundTripped.x;
        }

        return sum;
    }

    private static void AssertVec3BitsEqual(Vec3 expected, Vec3 actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.x), BitConverter.SingleToInt32Bits(actual.x));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.y), BitConverter.SingleToInt32Bits(actual.y));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.z), BitConverter.SingleToInt32Bits(actual.z));
    }
}
