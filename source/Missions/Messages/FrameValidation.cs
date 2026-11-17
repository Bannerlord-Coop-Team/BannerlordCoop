using System;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>The rule for a replicated hull or hook frame: finite, scene-bounded and an orthonormal right-handed rotation.</summary>
public static class FrameValidation
{
    private const float MaxCoordinate = 10000f;
    private const float Tolerance = 0.02f;

    public static bool IsValid(MatrixFrame frame)
    {
        var rotation = frame.rotation;
        if (!IsBounded(rotation.s) || !IsBounded(rotation.f) || !IsBounded(rotation.u) || !IsBounded(frame.origin)) return false;

        return Math.Abs(rotation.s.LengthSquared - 1) < Tolerance && Math.Abs(rotation.f.LengthSquared - 1) < Tolerance
            && Math.Abs(rotation.u.LengthSquared - 1) < Tolerance && Math.Abs(Vec3.DotProduct(rotation.s, rotation.f)) < Tolerance
            && Vec3.DotProduct(Vec3.CrossProduct(rotation.s, rotation.f), rotation.u) > 0.98f;
    }

    private static bool IsBounded(Vec3 vector) => IsBounded(vector.x) && IsBounded(vector.y) && IsBounded(vector.z);

    private static bool IsBounded(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= MaxCoordinate;
}
