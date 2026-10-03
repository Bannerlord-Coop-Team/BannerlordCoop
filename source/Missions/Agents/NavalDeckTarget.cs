#if DEBUG
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>Resolves the current local frame of the hull a deck pose was reported against.</summary>
public delegate bool NavalDeckFrameResolver(Agent agent, int deckShip, out MatrixFrame hullFrame);

/// <summary>
/// An owner pose kept in hull space so the puppet target follows the follower hull on every tick, including
/// between packets. The follower frame is interpolated, so the inverse tolerates a slightly non-orthonormal basis.
/// </summary>
internal readonly struct NavalDeckTarget
{
    public NavalDeckTarget(int ship, Vec3 local, in MatrixFrame receiveHull, Vec2 worldMovementDirection,
        Vec3 worldLookDirection)
    {
        var rotation = new MatrixFrame(receiveHull.rotation, Vec3.Zero);
        Ship = ship;
        Local = local;
        LocalMovementDirection = rotation.TransformToLocalNonOrthogonal(
            new Vec3(worldMovementDirection.X, worldMovementDirection.Y, 0f));
        LocalLookDirection = rotation.TransformToLocalNonOrthogonal(worldLookDirection);
    }

    public int Ship { get; }
    public Vec3 Local { get; }
    public Vec3 LocalMovementDirection { get; }
    public Vec3 LocalLookDirection { get; }

    public Vec3 ResolvePosition(in MatrixFrame hull) => hull.TransformToParent(Local);

    public Vec2 ResolveMovementDirection(in MatrixFrame hull)
    {
        Vec2 direction = hull.rotation.TransformToParent(LocalMovementDirection).AsVec2;
        if (direction.LengthSquared <= 0.0001f) return Vec2.Zero;
        direction.Normalize();
        return direction;
    }

    public Vec3 ResolveLookDirection(in MatrixFrame hull)
    {
        Vec3 look = hull.rotation.TransformToParent(LocalLookDirection);
        return look.LengthSquared <= 0.0001f ? look : look.NormalizedCopy();
    }

    public float HorizontalError(in MatrixFrame hull, Vec3 position) =>
        (hull.TransformToLocalNonOrthogonal(position) - Local).AsVec2.Length;
}
#endif
