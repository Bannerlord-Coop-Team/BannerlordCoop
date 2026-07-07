using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>
/// Feeds replicated puppet movement into the native input path every tick without making the puppet autonomous AI.
/// </summary>
public class PuppetMovementComponent : AgentComponent
{
    private const float InputThresholdSquared = 0.0025f;
    private const float TargetMoveThresholdSquared = 0.0025f;
    private const float FollowTargetThresholdSquared = 0.09f;

    private readonly bool hadAiInputCallback;

    private bool hasState;
    private bool hasPreviousTarget;
    private bool ownerTargetMoved;
    private Vec2 inputVector;
    private Vec2 movementDirection;
    private Vec3 lookDirection;
    private Vec3 targetPosition;
    private Agent.MovementControlFlag movementFlags;

    private PuppetMovementComponent(Agent agent) : base(agent)
    {
        hadAiInputCallback = agent.GetHasOnAiInputSetCallback();
        agent.SetHasOnAiInputSetCallback(true);
    }

    public static void Apply(Agent agent, Vec2 inputVector, Vec2 movementDirection, Vec3 lookDirection, Vec3 targetPosition)
    {
        if (agent == null || !agent.IsActive()) return;

        var component = agent.GetComponent<PuppetMovementComponent>();
        if (component == null)
        {
            component = new PuppetMovementComponent(agent);
            agent.AddComponent(component);
        }

        component.SetState(inputVector, movementDirection, lookDirection, targetPosition);
    }

    public override void OnTick(float dt)
    {
        ApplyState();
    }

    public override void OnAIInputSet(
        ref Agent.EventControlFlag eventFlag,
        ref Agent.MovementControlFlag movementFlag,
        ref Vec2 inputVector)
    {
        if (!hasState) return;

        inputVector = this.inputVector;
        movementFlag = (movementFlag & ~Agent.MovementControlFlag.MoveMask) | movementFlags;
    }

    public override void OnComponentRemoved()
    {
        if (Agent != null && Agent.IsActive())
            Agent.SetHasOnAiInputSetCallback(hadAiInputCallback);
    }

    private void SetState(Vec2 inputVector, Vec2 movementDirection, Vec3 lookDirection, Vec3 targetPosition)
    {
        ownerTargetMoved = hasPreviousTarget
            && this.targetPosition.DistanceSquared(targetPosition) > TargetMoveThresholdSquared;
        hasPreviousTarget = true;

        this.inputVector = ResolveInputVector(inputVector, movementDirection, targetPosition);
        this.movementDirection = movementDirection;
        this.lookDirection = lookDirection;
        this.targetPosition = targetPosition;
        movementFlags = ToMovementFlags(this.inputVector);
        hasState = true;

        ApplyState();
    }

    private Vec2 ResolveInputVector(Vec2 replicatedInput, Vec2 replicatedMovementDirection, Vec3 targetPosition)
    {
        if (replicatedInput.LengthSquared > InputThresholdSquared)
            return replicatedInput;

        bool shouldKeepMoving = ownerTargetMoved
            || Agent.Position.DistanceSquared(targetPosition) > FollowTargetThresholdSquared;
        if (!shouldKeepMoving || replicatedMovementDirection.LengthSquared <= InputThresholdSquared)
            return Vec2.Zero;

        return Vec2.Forward;
    }

    private void ApplyState()
    {
        if (!hasState || Agent == null || !Agent.IsActive()) return;

        Agent.SetMovementDirection(movementDirection);
        Agent.LookDirection = lookDirection;
        Agent.MovementInputVector = inputVector;
        Agent.MovementFlags = (Agent.MovementFlags & ~Agent.MovementControlFlag.MoveMask) | movementFlags;
    }

    private static Agent.MovementControlFlag ToMovementFlags(Vec2 input)
    {
        var flags = Agent.MovementControlFlag.None;

        if (input.Y > 0.05f)
            flags |= Agent.MovementControlFlag.Forward;
        else if (input.Y < -0.05f)
            flags |= Agent.MovementControlFlag.Backward;

        if (input.X > 0.05f)
            flags |= Agent.MovementControlFlag.StrafeRight;
        else if (input.X < -0.05f)
            flags |= Agent.MovementControlFlag.StrafeLeft;

        return flags;
    }
}
