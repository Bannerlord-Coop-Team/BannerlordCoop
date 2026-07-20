using Missions.Agents.Handlers;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

public class MockAgentVisualActionAccessor : IAgentVisualActionAccessor
{
    public bool TryGetAction(
        Agent agent,
        int channel,
        out ActionIndexCache action)
    {
        action = new ActionIndexCache(-1);
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.UseVisualSkeleton)
        {
            return false;
        }

        action = new ActionIndexCache(
            channel == 0
                ? mirror.SkeletonAction0Index
                : mirror.SkeletonAction1Index);
        return true;
    }

    public bool TryGetAnimationIndex(
        Agent agent,
        int channel,
        out int animationIndex)
    {
        animationIndex = -1;
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.UseVisualSkeleton)
        {
            return false;
        }

        if (!mirror.HideSkeletonAnimationIndex)
        {
            animationIndex = channel == 0
                ? mirror.SkeletonAnimation0Index
                : mirror.SkeletonAnimation1Index;
        }
        return true;
    }

    public bool TrySetAction(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress)
    {
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.UseVisualSkeleton)
        {
            return false;
        }

        if (channel == 0)
        {
            mirror.SkeletonAction0Index = action.Index;
            mirror.SkeletonAnimation0Index = action.Index;
            mirror.SkeletonAction0Parameter = progress;
        }
        else
        {
            mirror.SkeletonAction1Index = action.Index;
            mirror.SkeletonAnimation1Index = action.Index;
            mirror.SkeletonAction1Parameter = progress;
        }

        mirror.SetSkeletonActionChannelCalls++;
        mirror.LastSetSkeletonActionChannel = channel;
        mirror.LastSetSkeletonActionParameter = progress;
        return true;
    }
}
