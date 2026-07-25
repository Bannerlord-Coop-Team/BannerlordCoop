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

        return rawVisualAction == action.Index;
    }

    public void AdvanceActionIfAvailable(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress)
    {
        if (!AgentMirror.TryGet(agent, out MirrorAgent mirror)
            || !mirror.HasVisualSkeleton)
        {
            return;
        }

        int rawVisualAction = channel == 0
            ? mirror.RawVisualAction0Index
            : mirror.RawVisualAction1Index;
        if (rawVisualAction == action.Index)
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

        if (channel == 0)
        {
            mirror.RawVisualAction0Index = action.Index;
            mirror.RawVisualAction0Progress = progress;
        }
        else
        {
            mirror.RawVisualAction1Index = action.Index;
            mirror.RawVisualAction1Progress = progress;
        }

        mirror.AdvanceRawVisualActionCalls++;
        mirror.InstallRawVisualActionCalls++;
    }
}
