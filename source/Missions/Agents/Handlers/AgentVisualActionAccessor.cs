using System;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentVisualActionAccessor
{
    bool IsActionVisible(
        Agent agent,
        int channel,
        in ActionIndexCache action);
    bool TryGetAnimationState(
        Agent agent,
        int channel,
        out int animationIndex,
        out float progress,
        out float speed);
    int GetAnimationIndex(
        Agent agent,
        in ActionIndexCache action);
    float GetAnimationDuration(int animationIndex);
    void AdvanceActionIfAvailable(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress,
        bool installIfMissing = true);
    void AdvanceAnimationIfAvailable(
        Agent agent,
        int channel,
        int animationIndex,
        float progress,
        float speed);
}

public class AgentVisualActionAccessor : IAgentVisualActionAccessor
{
    private const float RetainedReactionAnimationBlendPeriod = -1f;
    // The publicized MBAPI field throws in some live runtime load contexts.
    private static readonly FieldInfo AnimationField =
        typeof(MBAPI).GetField(
            "IMBAnimation",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    private static MethodInfo getAnimationDuration;

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
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return false;

            animationIndex = skeleton.GetAnimationIndexAtChannel(channel);
            if (animationIndex < 0) return false;

            progress = skeleton.GetAnimationParameterAtChannel(channel);
            speed = skeleton.GetAnimationSpeedAtChannel(channel);
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

    public float GetAnimationDuration(int animationIndex)
    {
        try
        {
            object animation = AnimationField?.GetValue(null);
            if (animation == null) return 0f;

            if (getAnimationDuration == null)
            {
                getAnimationDuration = animation.GetType().GetMethod(
                    "GetAnimationDuration",
                    new[] { typeof(int) });
            }

            object duration = getAnimationDuration?.Invoke(
                animation,
                new object[] { animationIndex });
            return duration is float value ? value : 0f;
        }
        catch
        {
            return 0f;
        }
    }

    public int GetAnimationIndex(
        Agent agent,
        in ActionIndexCache action)
    {
        return MBActionSet.GetAnimationIndexOfAction(
            agent.ActionSet,
            in action);
    }

    public void AdvanceActionIfAvailable(
        Agent agent,
        int channel,
        in ActionIndexCache action,
        float progress,
        bool installIfMissing = true)
    {
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null)) return;

            int animationIndex = GetAnimationIndex(agent, in action);
            if (animationIndex < 0) return;

            AdvanceAnimation(
                skeleton,
                channel,
                animationIndex,
                progress,
                speed: 1f,
                installIfMissing);
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

    public void AdvanceAnimationIfAvailable(
        Agent agent,
        int channel,
        int animationIndex,
        float progress,
        float speed)
    {
        Skeleton skeleton = null;
        try
        {
            skeleton = GetSkeleton(agent);
            if (ReferenceEquals(skeleton, null) || animationIndex < 0) return;

            AdvanceAnimation(
                skeleton,
                channel,
                animationIndex,
                progress,
                speed,
                installIfMissing: true);
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

    private static void AdvanceAnimation(
        Skeleton skeleton,
        int channel,
        int animationIndex,
        float progress,
        float speed,
        bool installIfMissing)
    {
        int visualAnimation = skeleton.GetAnimationIndexAtChannel(channel);
        if (visualAnimation == animationIndex)
        {
            skeleton.SetAnimationParameterAtChannel(channel, progress);
            skeleton.SetAnimationSpeedAtChannel(channel, speed);
            return;
        }

        if (!installIfMissing) return;

        // Native can replace the puppet reaction before display. Present the retained clip at its
        // existing timeline without restarting another authored blend.
        skeleton.SetAnimationAtChannel(
            animationIndex,
            channel,
            animationSpeedMultiplier: speed,
            blendInPeriod: RetainedReactionAnimationBlendPeriod,
            startProgress: progress);
    }

    private static Skeleton GetSkeleton(Agent agent)
    {
        MBAgentVisuals visuals = agent.AgentVisuals;
        return !ReferenceEquals(visuals, null) && visuals.IsValid()
            ? visuals.GetSkeleton()
            : null;
    }
}
