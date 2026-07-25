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

    public bool TryGetAnimationState(
        Agent agent,
        int channel,
        out int animationIndex,
        out float progress,
        out float speed)
    {
        animationIndex = -1;
        progress = 0f;
        speed = 0f;
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.HasVisualSkeleton)
        {
            return false;
        }

        animationIndex = channel == 0
            ? mirror.RawVisualAction0Index
            : mirror.RawVisualAction1Index;
        progress = channel == 0
            ? mirror.RawVisualAction0Progress
            : mirror.RawVisualAction1Progress;
        speed = 1f;
        return animationIndex >= 0;
    }

    public float GetAnimationDuration(Agent agent, int animationIndex)
    {
        if (animationIndex < 0
            || !AgentMirror.TryGet(agent, out MirrorAgent mirror))
            return 0f;

        return mirror.AnimationDurations.TryGetValue(
            animationIndex,
            out float duration)
            ? duration
            : 1f;
    }

    public int GetAnimationIndex(
        Agent agent,
        in ActionIndexCache action)
    {
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror))
            return -1;

        return GetAnimationIndex(mirror, action.Index);
    }

    public void AdvanceExistingAnimationIfAvailable(
        Agent agent,
        int channel,
        int animationIndex,
        float progress,
        float speed)
    {
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.HasVisualSkeleton)
        {
            return;
        }

        AdvanceAnimation(
            mirror,
            channel,
            animationIndex,
            progress);
    }

    private static void AdvanceAnimation(
        MirrorAgent mirror,
        int channel,
        int animationIndex,
        float progress)
    {
        int rawVisualAction = channel == 0
            ? mirror.RawVisualAction0Index
            : mirror.RawVisualAction1Index;
        if (rawVisualAction == animationIndex)
        {
            if (channel == 0)
            {
                mirror.RawVisualAction0Progress = progress;
            }
            else
            {
                mirror.RawVisualAction1Progress = progress;
            }

            mirror.AdvanceRawVisualActionCalls++;
            mirror.AdvanceExistingRawVisualActionCalls++;
            return;
        }
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
