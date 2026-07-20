using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentVisualActionAccessor
{
    bool TryGetAction(Agent agent, int channel, out ActionIndexCache action);
    bool TryGetAnimationIndex(Agent agent, int channel, out int animationIndex);
    bool TrySetAction(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress);
}

public class AgentVisualActionAccessor : IAgentVisualActionAccessor
{
    public bool TryGetAction(
        Agent agent,
        int channel,
        out ActionIndexCache action)
    {
        action = new ActionIndexCache(-1);
        try
        {
            Skeleton skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return false;

            action = skeleton.GetActionAtChannel(channel);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryGetAnimationIndex(
        Agent agent,
        int channel,
        out int animationIndex)
    {
        animationIndex = -1;
        try
        {
            Skeleton skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return false;

            animationIndex = skeleton.GetAnimationIndexAtChannel(channel);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TrySetAction(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress)
    {
        try
        {
            Skeleton skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return false;

            skeleton.SetAgentActionChannel(
                channel,
                action,
                channelParameter: progress,
                forceFaceMorphRestart: false);
            return true;
        }
        catch
        {
            return false;
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
