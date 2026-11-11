using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>Resolves the current local frame of the hull a deck pose was reported against.</summary>
public delegate bool NavalDeckFrameResolver(Agent agent, Guid deckShip, out MatrixFrame hullFrame);

/// <summary>
/// An owner pose kept in hull space so the puppet target follows the local hull on every tick, including
/// between packets. The inverse tolerates a slightly non-orthonormal hull basis.
/// </summary>
internal readonly struct NavalDeckTarget
{
    // Keeps the pose and the owner's world directions relative to the hull frame they were received against.
    public NavalDeckTarget(Guid ship, Vec3 local, in MatrixFrame receiveHull, Vec2 worldMovementDirection,
        Vec3 worldLookDirection)
    {
        var rotation = new MatrixFrame(receiveHull.rotation, Vec3.Zero);
        Ship = ship;
        Local = local;
        LocalMovementDirection = rotation.TransformToLocalNonOrthogonal(
            new Vec3(worldMovementDirection.X, worldMovementDirection.Y, 0f));
        LocalLookDirection = rotation.TransformToLocalNonOrthogonal(worldLookDirection);
    }

    public Guid Ship { get; }
    public Vec3 Local { get; }
    public Vec3 LocalMovementDirection { get; }
    public Vec3 LocalLookDirection { get; }

    // The pose's world position on the hull's current frame.
    public Vec3 ResolvePosition(in MatrixFrame hull) => hull.TransformToParent(Local);

    // The owner's movement direction on the hull's current frame, or zero when it has none.
    public Vec2 ResolveMovementDirection(in MatrixFrame hull)
    {
        Vec2 direction = hull.rotation.TransformToParent(LocalMovementDirection).AsVec2;
        if (direction.LengthSquared <= 0.0001f) return Vec2.Zero;
        direction.Normalize();
        return direction;
    }

    // The owner's look direction on the hull's current frame.
    public Vec3 ResolveLookDirection(in MatrixFrame hull)
    {
        Vec3 look = hull.rotation.TransformToParent(LocalLookDirection);
        return look.LengthSquared <= 0.0001f ? look : look.NormalizedCopy();
    }

    // How far a puppet at the world position stands from this pose, measured on the hull's deck plane.
    public float HorizontalError(in MatrixFrame hull, Vec3 position) => HorizontalError(hull, Local, position);

    /// <summary>The horizontal distance between a hull-local spot and a world position, measured in the hull's frame so height
    /// above the deck doesn't count.</summary>
    internal static float HorizontalError(in MatrixFrame hull, Vec3 local, Vec3 position) =>
        (hull.TransformToLocalNonOrthogonal(position) - local).AsVec2.Length;
}
