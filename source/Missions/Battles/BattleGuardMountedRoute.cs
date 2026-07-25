#if DEBUG
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Drives the mounted guard around a bounded lane with native player input.</summary>
internal sealed class BattleGuardMountedRoute
{
    private const float EndpointTurnDistance = 6f;
    internal const float MinimumLength = EndpointTurnDistance * 2f;
    private const float StrikeClearanceDistance = 16f;
    private const float StrikeMaximumLateralOffset = 3f;
    private const float StrikeAlignmentDot = 0.9f;
    private const float TurnCompletionDot = 0.65f;
    private const float TurnForwardInput = 0.45f;
    private const float SteeringGain = 2f;
    private const float SteeringDeadZone = 0.05f;

    private readonly Vec3 start;
    private readonly Vec3 end;
    private readonly Vec3 axis;
    private readonly float length;
    private bool headingToEnd = true;
    private bool turning;
    private bool initialized;

    public float Progress { get; private set; }
    public float LateralOffset { get; private set; }
    public float RemainingDistance { get; private set; }
    public int CompletedTurns { get; private set; }
    public bool CanStageStrike { get; private set; }
    public Vec3 Start => start;
    public Vec3 Direction => axis;
    public float Length => length;
    public string State =>
        !initialized
            ? "Pending"
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
        if (!initialized)
        {
            if (Progress < -EndpointTurnDistance ||
                Progress > length + EndpointTurnDistance ||
                Math.Abs(LateralOffset) > EndpointTurnDistance)
            {
                RemainingDistance = 0f;
                CanStageStrike = false;
                return new BattleGuardMountedRouteInput(
                    Vec2.Zero,
                    Agent.MovementControlFlag.None);
            }

            headingToEnd =
                Vec3.DotProduct(heading, axis) >= 0f;
            initialized = true;
        }

        if (!turning &&
            ((headingToEnd &&
              Progress >= length - EndpointTurnDistance) ||
             (!headingToEnd &&
              Progress <= EndpointTurnDistance)))
        {
            headingToEnd = !headingToEnd;
            turning = true;
        }

        Vec3 target = headingToEnd ? end : start;
        Vec3 desired = target - position;
        desired.z = 0f;
        if (desired.LengthSquared < 0.0001f)
            desired = headingToEnd ? axis : -axis;
        else
            desired.Normalize();

        float alignment = Vec3.DotProduct(heading, desired);
        float signedError =
            (heading.x * desired.y) -
            (heading.y * desired.x);
        if (turning && alignment >= TurnCompletionDot)
        {
            turning = false;
            CompletedTurns++;
        }

        float turnInput;
        float forwardInput;
        if (turning)
        {
            turnInput = 1f;
            forwardInput = TurnForwardInput;
        }
        else
        {
            turnInput = Clamp(-signedError * SteeringGain);
            forwardInput = 1f;
        }

        RemainingDistance = headingToEnd
            ? length - Progress
            : Progress;
        CanStageStrike =
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
            turnFlag);
    }

    private static float Clamp(float value)
    {
        return Math.Max(-1f, Math.Min(1f, value));
    }
}

internal readonly struct BattleGuardMountedRouteInput
{
    public Vec2 Movement { get; }
    public Agent.MovementControlFlag TurnFlag { get; }

    public BattleGuardMountedRouteInput(
        Vec2 movement,
        Agent.MovementControlFlag turnFlag)
    {
        Movement = movement;
        TurnFlag = turnFlag;
    }
}
#endif
