using Missions.Agents.Handlers;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

public class MockAgentVisualActionAccessor : IAgentVisualActionAccessor
{
    public bool IsActionVisible(
        Agent agent,
        int channel,
        in ActionIndexCache action)
    {
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.HasVisualSkeleton)
        {
            return false;
        }

        int visualAction = channel == 0
            ? mirror.SkeletonAction0Index
            : mirror.SkeletonAction1Index;
        int rawVisualAction = channel == 0
            ? mirror.RawVisualAction0Index
            : mirror.RawVisualAction1Index;
        if (visualAction == action.Index)
            return true;
        if (visualAction != ActionIndexCache.act_none.Index) return false;

        return rawVisualAction == GetAnimationIndex(mirror, action.Index);
    }

    public bool HasVisibleAction(
        Agent agent,
        int channel)
    {
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.HasVisualSkeleton)
        {
            return false;
        }

        int visualAction = channel == 0
            ? mirror.SkeletonAction0Index
            : mirror.SkeletonAction1Index;
        int rawVisualAction = channel == 0
            ? mirror.RawVisualAction0Index
            : mirror.RawVisualAction1Index;
        return visualAction != ActionIndexCache.act_none.Index
            || rawVisualAction >= 0;
    }

    private static int GetAnimationIndex(
        MirrorAgent mirror,
        int actionIndex)
    {
        return mirror.ActionAnimationIndices.TryGetValue(
            actionIndex,
            out int animationIndex)
            ? animationIndex
            : actionIndex;
    }
}
