using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
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
        float progress,
        bool replaceCurrentVisual);
}

public class AgentVisualActionAccessor : IAgentVisualActionAccessor
{
    private static readonly ILogger Logger =
        LogManager.GetLogger<AgentVisualActionAccessor>();
    private const float RetainedGuardAnimationBlendPeriod = -0.2f;
    private const float ReplayLogIntervalSeconds = 1f;

    private readonly Dictionary<long, float> _lastReplayLogTimes =
        new Dictionary<long, float>();

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
                return animationIndex < 0 || visualAnimation == animationIndex;
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
        float progress,
        bool replaceCurrentVisual)
    {
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return;

            ActionIndexCache visualAction = skeleton.GetActionAtChannel(channel);
            int animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            if (animationIndex < 0) return;

            int visualAnimation = skeleton.GetAnimationIndexAtChannel(channel);
            if (visualAction == action
                && visualAnimation == animationIndex)
            {
                skeleton.SetAnimationParameterAtChannel(channel, progress);
                return;
            }

            if (visualAction == ActionIndexCache.act_none
                && visualAnimation >= 0
                && !replaceCurrentVisual)
            {
                return;
            }

            if (visualAction != ActionIndexCache.act_none
                && visualAction != action
                && !replaceCurrentVisual)
            {
                return;
            }

            LogVisualReplay(
                agent,
                channel,
                action,
                visualAction,
                animationIndex,
                visualAnimation);

            // Keep the authored agent action layer, including its mounted blending, instead of forcing
            // the action's raw animation over the rider pose selected by the horse pace.
            skeleton.SetAgentActionChannel(
                channel,
                in action,
                channelParameter: progress,
                blendPeriodOverride: RetainedGuardAnimationBlendPeriod,
                forceFaceMorphRestart: false);
            skeleton.TickActionChannels();
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

    private void LogVisualReplay(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        in ActionIndexCache visualAction,
        int animationIndex,
        int visualAnimation)
    {
        float now = Mission.Current?.CurrentTime ?? 0f;
        long key = ((long)agent.Index << 2) | (uint)channel;
        if (_lastReplayLogTimes.TryGetValue(key, out float last)
            && now - last < ReplayLogIntervalSeconds)
        {
            return;
        }

        _lastReplayLogTimes[key] = now;
        Logger.Debug(
            "[GuardSync] Reinstall visual agent={Agent} mounted={Mounted} " +
            "channel={Channel} action={Action} visualAction={VisualAction} " +
            "animation={Animation} visualAnimation={VisualAnimation}",
            agent.Index,
            agent.HasMount,
            channel,
            action.Index,
            visualAction.Index,
            animationIndex,
            visualAnimation);
    }

    private static Skeleton GetSkeleton(Agent agent)
    {
        MBAgentVisuals visuals = agent.AgentVisuals;
        return !ReferenceEquals(visuals, null) && visuals.IsValid()
            ? visuals.GetSkeleton()
            : null;
    }
}
