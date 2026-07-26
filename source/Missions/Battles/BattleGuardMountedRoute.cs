#if DEBUG
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Drives the mounted guard around a bounded lane with native player input.</summary>
internal sealed class BattleGuardMountedRoute
{
    private const float PositionToleranceDistance = 6f;
    internal const float StrikeClearanceDistance = 16f;
    internal const float MinimumLength = StrikeClearanceDistance * 2f;
    private const float StrikeMaximumLateralOffset = 3f;
    private const float StrikeAlignmentDot = 0.9f;
    private const float TurnCompletionDot = 0.9f;
    private const float TurnResumeMaximumSpeed = 1f;
    private const float TurnBrakeMinimumSpeed = 1.5f;
    private const float TurnForwardInput = 0.2f;
    private const float SteeringGain = 2f;
    private const float SteeringDeadZone = 0.05f;
    private const float MovementHeadingMinimumSpeed = 0.1f;

    private readonly Vec3 start;
    private readonly Vec3 end;
    private readonly Vec3 axis;
    private readonly float length;
    private bool headingToEnd = true;
    private bool braking;
    private bool turning;
    private bool initialized;

    public float Progress { get; private set; }
    public float LateralOffset { get; private set; }
    public float RemainingDistance { get; private set; }
    public int CompletedTurns { get; private set; }
    public bool CanStageStrike { get; private set; }
    public bool IsHeadingToEnd => headingToEnd;
    public Vec3 Start => start;
    public Vec3 Direction => axis;
    public float Length => length;
    public string State =>
        !initialized
            ? "Pending"
            : braking
            ? headingToEnd ? "BrakingToEnd" : "BrakingToStart"
            : turning
            ? headingToEnd ? "TurningToEnd" : "TurningToStart"
            : headingToEnd ? "Forward" : "Return";

    public BattleGuardMountedRoute(
        Vec3 start,
        Vec3 direction,
        float length)
    {
        direction.z = 0f;
        if (direction.LengthSquared < 0.0001f)
            throw new ArgumentException("route direction is required", nameof(direction));
        if (length <= MinimumLength)
            throw new ArgumentOutOfRangeException(nameof(length));

        direction.Normalize();
        this.start = start;
        axis = direction;
        this.length = length;
        end = start + (axis * length);
    }

    public BattleGuardMountedRouteInput Update(
        Vec3 position,
        Vec3 heading)
    {
        return Update(
            position,
            heading,
            heading,
            0f);
    }

    private BattleGuardMountedRouteInput Update(
        Vec3 position,
        Vec3 heading,
        Vec3 facing,
        float horizontalSpeed)
    {
        Vec3 offset = position - start;
        offset.z = 0f;
        Progress = Vec3.DotProduct(offset, axis);
        LateralOffset =
            (axis.x * offset.y) -
            (axis.y * offset.x);

        heading.z = 0f;
        if (heading.LengthSquared < 0.0001f)
            heading = headingToEnd ? axis : -axis;
        else
            heading.Normalize();
        facing.z = 0f;
        if (facing.LengthSquared < 0.0001f)
            facing = heading;
        else
            facing.Normalize();
        if (!initialized)
        {
            if (Progress < -PositionToleranceDistance ||
                Progress > length + PositionToleranceDistance ||
                Math.Abs(LateralOffset) > PositionToleranceDistance)
            {
                RemainingDistance = 0f;
                CanStageStrike = false;
                return new BattleGuardMountedRouteInput(
                    Vec2.Zero,
                    Agent.MovementControlFlag.None,
                    Agent.MovementControlFlag.None);
            }

            headingToEnd =
                Vec3.DotProduct(heading, axis) >= 0f;
            initialized = true;
        }

        if (!braking &&
            !turning &&
            ((headingToEnd &&
              Progress >= length - StrikeClearanceDistance) ||
             (!headingToEnd &&
              Progress <= StrikeClearanceDistance)))
        {
            headingToEnd = !headingToEnd;
            braking = horizontalSpeed > TurnResumeMaximumSpeed;
            turning = true;
        }

        Vec3 target = headingToEnd ? end : start;
        Vec3 desired = target - position;
        desired.z = 0f;
        if (desired.LengthSquared < 0.0001f)
            desired = headingToEnd ? axis : -axis;
        else
            desired.Normalize();

        Vec3 steeringHeading =
            braking || turning ? facing : heading;
        float alignment =
            Vec3.DotProduct(steeringHeading, desired);
        float signedError =
            (steeringHeading.x * desired.y) -
            (steeringHeading.y * desired.x);
        if (turning &&
            !braking &&
            horizontalSpeed >= TurnBrakeMinimumSpeed)
        {
            braking = true;
        }
        if (braking &&
            horizontalSpeed <= TurnResumeMaximumSpeed)
        {
            braking = false;
        }
        if (turning && alignment >= TurnCompletionDot)
        {
            braking = false;
            turning = false;
            CompletedTurns++;
        }

        float turnInput;
        float forwardInput;
        Agent.MovementControlFlag translationFlag;
        if (braking)
        {
            turnInput = 1f;
            forwardInput = -1f;
            translationFlag =
                Agent.MovementControlFlag.Backward;
        }
        else if (turning)
        {
            turnInput = 1f;
            forwardInput = TurnForwardInput;
            translationFlag =
                Agent.MovementControlFlag.Forward;
        }
        else
        {
            turnInput = Clamp(-signedError * SteeringGain);
            forwardInput = 1f;
            translationFlag =
                Agent.MovementControlFlag.Forward;
        }

        RemainingDistance = headingToEnd
            ? length - Progress
            : Progress;
        CanStageStrike =
            !braking &&
            !turning &&
            Progress >= 0f &&
            Progress <= length &&
            RemainingDistance >= StrikeClearanceDistance &&
            Math.Abs(LateralOffset) <= StrikeMaximumLateralOffset &&
            alignment >= StrikeAlignmentDot;

        Agent.MovementControlFlag turnFlag =
            Agent.MovementControlFlag.None;
        if (turnInput > SteeringDeadZone)
            turnFlag = Agent.MovementControlFlag.TurnRight;
        else if (turnInput < -SteeringDeadZone)
            turnFlag = Agent.MovementControlFlag.TurnLeft;

        return new BattleGuardMountedRouteInput(
            new Vec2(turnInput, forwardInput),
            translationFlag,
            turnFlag);
    }

    public BattleGuardMountedRouteInput Update(
        Vec3 position,
        Vec2 movementDirection,
        Vec3 lookDirection,
        Vec3 physicalFacing,
        float horizontalSpeed)
    {
        return Update(
            position,
            ResolveHeading(
                movementDirection,
                lookDirection,
                horizontalSpeed),
            physicalFacing,
            horizontalSpeed);
    }

    internal static Vec3 ResolveHeading(
        Vec2 movementDirection,
        Vec3 lookDirection,
        float horizontalSpeed)
    {
        Vec3 heading = horizontalSpeed >= MovementHeadingMinimumSpeed
            ? new Vec3(
                movementDirection.x,
                movementDirection.y,
                0f)
            : Vec3.Zero;
        if (heading.LengthSquared < 0.0001f)
        {
            heading = lookDirection;
            heading.z = 0f;
        }
        if (heading.LengthSquared >= 0.0001f)
            heading.Normalize();
        return heading;
    }

    private static float Clamp(float value)
    {
        return Math.Max(-1f, Math.Min(1f, value));
    }
}

internal readonly struct BattleGuardMountedRouteInput
{
    public Vec2 Movement { get; }
    public Agent.MovementControlFlag TranslationFlag { get; }
    public Agent.MovementControlFlag TurnFlag { get; }

    public BattleGuardMountedRouteInput(
        Vec2 movement,
        Agent.MovementControlFlag translationFlag,
        Agent.MovementControlFlag turnFlag)
    {
        Movement = movement;
        TranslationFlag = translationFlag;
        TurnFlag = turnFlag;
    }
}
#endif
