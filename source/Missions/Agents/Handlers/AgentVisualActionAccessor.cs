using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentVisualActionAccessor
{
    bool IsActionVisible(
        Agent agent,
        int channel,
        in ActionIndexCache action);
    void AdvanceActionIfAvailable(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress);
}

public class AgentVisualActionAccessor : IAgentVisualActionAccessor
{
    private const float RetainedGuardAnimationBlendPeriod = -1f;

    public bool IsActionVisible(
        Agent agent,
        int channel,
        in ActionIndexCache action)
    {
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return false;

            ActionIndexCache visualAction = skeleton.GetActionAtChannel(channel);
            if (visualAction == action) return true;
            if (visualAction != ActionIndexCache.act_none) return false;

            int animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            return animationIndex >= 0
                && skeleton.GetAnimationIndexAtChannel(channel) == animationIndex;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    public void AdvanceActionIfAvailable(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress)
    {
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return;

            ActionIndexCache visualAction = skeleton.GetActionAtChannel(channel);
            if (visualAction != ActionIndexCache.act_none
                && visualAction != action)
            {
                return;
            }

            int animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            if (animationIndex < 0) return;

            int visualAnimation = skeleton.GetAnimationIndexAtChannel(channel);
            if (visualAction == ActionIndexCache.act_none
                && visualAnimation >= 0
                && visualAnimation != animationIndex)
            {
                return;
            }

            // Native ticking removes the puppet action before it can blend. Keep its visual clip
            // independent and re-arm the authored blend without resetting progress.
            skeleton.SetAnimationAtChannel(
                animationIndex,
                channel,
                animationSpeedMultiplier: 1f,
                blendInPeriod: RetainedGuardAnimationBlendPeriod,
                startProgress: progress);
        }
        catch
        {
            // Visuals can become invalid between the validity check and skeleton access.
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    private static Skeleton GetSkeleton(Agent agent)
    {
        MBAgentVisuals visuals = agent.AgentVisuals;
        return !ReferenceEquals(visuals, null) && visuals.IsValid()
            ? visuals.GetSkeleton()
            : null;
    }
}
