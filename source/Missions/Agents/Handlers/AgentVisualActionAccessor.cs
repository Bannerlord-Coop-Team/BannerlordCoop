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
    private const float RetainedReactionAnimationBlendPeriod = -1f;

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
            int animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            int visualAnimation =
                skeleton.GetAnimationIndexAtChannel(channel);
            if (visualAction == action)
                return true;
            if (visualAction != ActionIndexCache.act_none) return false;

            return animationIndex >= 0 && visualAnimation == animationIndex;
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

            int animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            if (animationIndex < 0) return;

            int visualAnimation = skeleton.GetAnimationIndexAtChannel(channel);
            if (visualAnimation == animationIndex)
            {
                skeleton.SetAnimationParameterAtChannel(channel, progress);
                return;
            }

            // Native can replace the puppet reaction before display. Present the retained clip at its
            // existing timeline without restarting another authored blend.
            skeleton.SetAnimationAtChannel(
                animationIndex,
                channel,
                animationSpeedMultiplier: 1f,
                blendInPeriod: RetainedReactionAnimationBlendPeriod,
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
