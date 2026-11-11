using Missions.Messages;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class FrameValidationTests
{
    public static IEnumerable<object[]> Frames()
    {
        yield return new object[] { "identity", MatrixFrame.Identity, true };
        yield return new object[] { "rotated and moved", Rotated(new Vec3(120f, -40f, 3f)), true };
        yield return new object[] { "at the coordinate bound", Moved(new Vec3(10000f, -10000f, 0f)), true };
        yield return new object[] { "past the coordinate bound", Moved(new Vec3(10000.5f, 0f, 0f)), false };
        yield return new object[] { "NaN origin", Moved(new Vec3(float.NaN, 0f, 0f)), false };
        yield return new object[] { "infinite origin", Moved(new Vec3(0f, float.PositiveInfinity, 0f)), false };
        yield return new object[] { "NaN rotation", WithRotation(new Mat3(new Vec3(float.NaN, 0f, 0f), Vec3.Forward, Vec3.Up)), false };
        yield return new object[] { "scaled rotation", WithRotation(new Mat3(new Vec3(1.2f, 0f, 0f), Vec3.Forward, Vec3.Up)), false };
        yield return new object[] { "skewed rotation", WithRotation(new Mat3(new Vec3(1f, 0.1f, 0f), Vec3.Forward, Vec3.Up)), false };
        yield return new object[] { "mirrored rotation", WithRotation(new Mat3(Vec3.Side, Vec3.Forward, -Vec3.Up)), false };
    }

    [Theory]
    [MemberData(nameof(Frames))]
    public void IsValid_AcceptsOnlyAFiniteBoundedOrthonormalRightHandedFrame(string name, MatrixFrame frame, bool expected)
    {
        Assert.True(expected == FrameValidation.IsValid(frame), name);
    }

    // The wire form keeps the same rule, so a sample frame and the deck frame it came from agree.
    [Theory]
    [MemberData(nameof(Frames))]
    public void IsValidFrame_AgreesWithTheMatrixFrameRule(string name, MatrixFrame frame, bool expected)
    {
        Assert.True(expected == NetworkBattleShipSample.IsValidFrame(NetworkBattleShipSample.FromFrame(frame)), name);
    }

    [Fact]
    public void IsValidFrame_RejectsAMissingOrShortFrame()
    {
        Assert.False(NetworkBattleShipSample.IsValidFrame(null));
        Assert.False(NetworkBattleShipSample.IsValidFrame(new float[NetworkBattleShipSample.FrameLength - 1]));
    }

    private static MatrixFrame Moved(Vec3 origin)
    {
        var frame = MatrixFrame.Identity;
        frame.origin = origin;
        return frame;
    }

    private static MatrixFrame Rotated(Vec3 origin)
    {
        var frame = Moved(origin);
        frame.rotation.RotateAboutUp(0.7f);
        return frame;
    }

    private static MatrixFrame WithRotation(Mat3 rotation)
    {
        var frame = MatrixFrame.Identity;
        frame.rotation = rotation;
        return frame;
    }
}
