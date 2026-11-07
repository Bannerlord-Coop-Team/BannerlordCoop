#if DEBUG
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>Builds a short, moving-target approach from the real ram tip and target body bounds.</summary>
public interface INavalRamStaging
{
    /// <summary>Returns an upright bow-on pose and a velocity that closes on the target's moving broadside.</summary>
    (MatrixFrame Frame, Vec3 Velocity) Approach(MatrixFrame target, Vec3 boundsMin, Vec3 boundsMax,
        Vec3 ramTipLocal, float ramRadius, Vec3 targetVelocity);
}

/// <inheritdoc cref="INavalRamStaging"/>
public sealed class NavalRamStaging : INavalRamStaging
{
    internal const float Gap = 6f;
    internal const float ClosingSpeed = 10f;

    /// <inheritdoc/>
    public (MatrixFrame Frame, Vec3 Velocity) Approach(MatrixFrame target, Vec3 boundsMin, Vec3 boundsMax,
        Vec3 ramTipLocal, float ramRadius, Vec3 targetVelocity)
    {
        var side = target.rotation.f.AsVec2.Normalized().RightVec().ToVec3();
        var frame = MatrixFrame.Identity;
        frame.rotation.f = -side;
        frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        var broadside = target.TransformToParent(new Vec3(boundsMax.x, (boundsMin.y + boundsMax.y) / 2f, 0f));
        // Offset the actual capsule tip, including an off-centre ram, rather than half the deployment-area length.
        frame.origin = broadside + (side * (Gap + ramRadius)) - frame.rotation.TransformToParent(ramTipLocal);
        frame.origin.z = target.origin.z;
        var velocity = new Vec3(targetVelocity.x, targetVelocity.y, 0f) + (frame.rotation.f * ClosingSpeed);
        return (frame, velocity);
    }
}
#endif
