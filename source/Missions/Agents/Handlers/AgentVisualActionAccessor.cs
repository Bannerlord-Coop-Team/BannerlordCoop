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

    bool HasVisibleAction(
        Agent agent,
        int channel);

    bool TrySetAction(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress,
        float blendPeriodOverride);
}

public class AgentVisualActionAccessor : IAgentVisualActionAccessor
{
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

    public bool HasVisibleAction(
        Agent agent,
        int channel)
    {
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return false;

            return skeleton.GetActionAtChannel(channel)
                    != ActionIndexCache.act_none
                || skeleton.GetAnimationIndexAtChannel(channel) >= 0;
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

    public bool TrySetAction(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress,
        float blendPeriodOverride)
    {
        Skeleton skeleton = null;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (ReferenceEquals(visuals, null) || !visuals.IsValid())
                return false;

            skeleton = visuals.GetSkeleton();
            if (ReferenceEquals(skeleton, null)) return false;

            if (IsActionVisible(agent, skeleton, channel, in action))
            {
                skeleton.SetAnimationParameterAtChannel(
                    channel,
                    Math.Max(0f, Math.Min(1f, progress)));
                skeleton.SetAnimationSpeedAtChannel(channel, 1f);
            }
            else
            {
                visuals.SetAgentActionChannel(
                    channel,
                    action.Index,
                    channelParameter:
                        Math.Max(0f, Math.Min(1f, progress)),
                    blendPeriodOverride: blendPeriodOverride,
                    forceFaceMorphRestart: false);
            }

            return true;
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

    private static bool IsActionVisible(
        Agent agent,
        Skeleton skeleton,
        int channel,
        in ActionIndexCache action)
    {
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

    private static Skeleton GetSkeleton(Agent agent)
    {
        MBAgentVisuals visuals = agent.AgentVisuals;
        return !ReferenceEquals(visuals, null) && visuals.IsValid()
            ? visuals.GetSkeleton()
            : null;
    }
}
