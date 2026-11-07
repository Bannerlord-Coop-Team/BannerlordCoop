using Missions.Missiles.Message;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class MissileOrientationCodecTests
{
    [Fact]
    public void RandomRotations_UnpackWellInsideTheTolerance()
    {
        var random = new Random(4498);
        float worstElementError = 0f;
        float worstPointShift = 0f;
        for (int i = 0; i < 20_000; i++)
        {
            Mat3 orientation = RandomRotation(random);

            Assert.True(MissileOrientationCodec.TryPack(orientation, out ulong packed));
            Mat3 unpacked = MissileOrientationCodec.Unpack(packed);

            worstElementError = Math.Max(worstElementError, MaxElementError(orientation, unpacked));
            // A point on a long javelin, 1.5 m from the missile origin and off its axis.
            Vec3 local = new Vec3(0.05f, 1.5f, 0.05f);
            worstPointShift = Math.Max(worstPointShift,
                (orientation.TransformToParent(local) - unpacked.TransformToParent(local)).Length);
        }

        // 20-bit components keep the frame within float noise, far under vanilla's own 0.002 missile frame step.
        Assert.InRange(worstElementError, 0f, 0.00002f);
        // Under 0.05 mm at the tip, so the flight and stuck pose look the same.
        Assert.InRange(worstPointShift, 0f, 0.00005f);
    }

    [Fact]
    public void AxisAlignedAndHalfTurnFrames_RoundTrip()
    {
        // Each one makes a different quaternion component the largest, including a negative one.
        var frames = new[]
        {
            Mat3.Identity,
            new Mat3(new Vec3(-1f, 0f, 0f), new Vec3(0f, -1f, 0f), new Vec3(0f, 0f, 1f)),
            new Mat3(new Vec3(1f, 0f, 0f), new Vec3(0f, -1f, 0f), new Vec3(0f, 0f, -1f)),
            new Mat3(new Vec3(-1f, 0f, 0f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, -1f)),
            new Mat3(new Vec3(0f, -1f, 0f), new Vec3(1f, 0f, 0f), new Vec3(0f, 0f, 1f)),
        };

        foreach (Mat3 frame in frames)
        {
            Assert.True(MissileOrientationCodec.TryPack(frame, out ulong packed));
            Assert.True(frame.NearlyEquals(MissileOrientationCodec.Unpack(packed), 0.00002f));
        }
    }

    [Fact]
    public void FramesThatArentRotations_DontPack()
    {
        var scaled = new Mat3(new Vec3(1.2f, 0f, 0f), new Vec3(0f, 1.2f, 0f), new Vec3(0f, 0f, 1.2f));
        var mirrored = new Mat3(Vec3.Side, Vec3.Forward, -Vec3.Up);
        var skewed = new Mat3(new Vec3(1f, 0.1f, 0f), Vec3.Forward, Vec3.Up);
        var notANumber = new Mat3(new Vec3(float.NaN, 0f, 0f), Vec3.Forward, Vec3.Up);

        Assert.False(MissileOrientationCodec.TryPack(scaled, out _));
        Assert.False(MissileOrientationCodec.TryPack(mirrored, out _));
        Assert.False(MissileOrientationCodec.TryPack(skewed, out _));
        Assert.False(MissileOrientationCodec.TryPack(notANumber, out _));
    }

    internal static Mat3 RandomRotation(Random random)
    {
        Mat3 orientation = Mat3.Identity;
        orientation.RotateAboutUp(NextAngle(random));
        orientation.RotateAboutSide(NextAngle(random));
        orientation.RotateAboutForward(NextAngle(random));
        return orientation;
    }

    private static float NextAngle(Random random) => (float)((random.NextDouble() * 2.0 - 1.0) * Math.PI);

    private static float MaxElementError(Mat3 expected, Mat3 actual) =>
        Math.Max(MaxElementError(expected.s, actual.s),
            Math.Max(MaxElementError(expected.f, actual.f), MaxElementError(expected.u, actual.u)));

    private static float MaxElementError(Vec3 expected, Vec3 actual) =>
        Math.Max(Math.Abs(expected.x - actual.x),
            Math.Max(Math.Abs(expected.y - actual.y), Math.Abs(expected.z - actual.z)));
}
