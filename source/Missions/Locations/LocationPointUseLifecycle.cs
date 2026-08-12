using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Locations;

/// <summary>
/// Restarts a replicated scene-point use from the point's canonical pre-arrival frame. The public
/// stop/use lifecycle resets AnimationPoint's private state, locks, pair state and item state; the
/// point then owns starting its configured arrive action and transitioning into its loop.
/// </summary>
internal static class LocationPointUseLifecycle
{
    public static void RestartFromCanonicalFrame(Agent agent, UsableMissionObject point)
    {
        if (agent == null || point == null || !agent.IsActive()) return;

        // Stop first. A displacement AnimationPoint unlocks its user frame while it is in use, so
        // GetUserFrameForAgent can otherwise return the agent's already-displaced world frame rather
        // than the scene point's canonical arrival frame. OnUseStopped restores those locks.
        if (agent.CurrentlyUsedGameObject != null)
            agent.StopUsingGameObject(isSuccessful: true);

        WorldFrame frame = point.GetUserFrameForAgent(agent);
        Vec2 direction = frame.Rotation.f.AsVec2.Normalized();

        // AnimationPoint.OnUseStopped may install its high-priority leave action. Clear the base
        // channel before immediate reuse so the point's normal arrive action cannot be rejected on
        // its first tick. Clear the upper channel too: paired greetings run there and must not leak
        // across a restarted pair lifecycle.
        agent.SetActionChannel(
            0,
            ActionIndexCache.act_none,
            ignorePriority: true,
            additionalFlags: AnimFlags.anf_restart,
            forceFaceMorphRestart: false);
        agent.SetActionChannel(
            1,
            ActionIndexCache.act_none,
            ignorePriority: true,
            additionalFlags: AnimFlags.anf_restart,
            forceFaceMorphRestart: false);
        agent.MovementInputVector = Vec2.Zero;
        agent.TeleportToPosition(frame.Origin.GetGroundVec3());
        agent.SetMovementDirection(in direction);

        // Do not call the sit action directly. OnUse resets AnimationPoint's lifecycle and the
        // point starts its own configured arrive action, root motion, loop, items and pair behavior.
        agent.UseGameObject(point);
        agent.SetTargetPositionAndDirection(frame.Origin.AsVec2, in frame.Rotation.f);
    }
}
