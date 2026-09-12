using System;

namespace Missions.Agents.Handlers;

public interface IMovementPriorityScheduler
{
    MovementPriorityKey CreateKey(
        bool isLocalMainAgent,
        float? distanceToRecipientFocus,
        float currentTime,
        float? lastSuccessfulSendTime,
        float pendingSince,
        Guid agentId);

    int Compare(MovementPriorityKey left, MovementPriorityKey right);
}

/// <summary>Stable per-recipient ordering key for one current movement snapshot.</summary>
public readonly struct MovementPriorityKey
{
    public int Tier { get; }
    public double Score { get; }
    public float LastSuccessfulSendTime { get; }
    public float PendingSince { get; }
    public Guid AgentId { get; }

    public MovementPriorityKey(
        int tier,
        double score,
        float lastSuccessfulSendTime,
        float pendingSince,
        Guid agentId)
    {
        Tier = tier;
        Score = score;
        LastSuccessfulSendTime = lastSuccessfulSendTime;
        PendingSince = pendingSince;
        AgentId = agentId;
    }
}

/// <summary>Ranks current snapshots by local-player tier, distance and time since successful delivery.</summary>
public sealed class MovementPriorityScheduler : IMovementPriorityScheduler
{
    public const float InterestRadius = 75f;
    public const double DistanceWeight = 7d;
    public const double StalenessHalfLifeSeconds = 0.075d;
    public const float MaximumPriorityAgingSeconds = 0.225f;

    public MovementPriorityKey CreateKey(
        bool isLocalMainAgent,
        float? distanceToRecipientFocus,
        float currentTime,
        float? lastSuccessfulSendTime,
        float pendingSince,
        Guid agentId)
    {
        float normalizedDistance = distanceToRecipientFocus.HasValue
            ? Math.Max(0f, Math.Min(1f, distanceToRecipientFocus.Value / InterestRadius))
            : 1f;
        double distanceComponent = 1d + (DistanceWeight * normalizedDistance);

        float effectiveLastSent = lastSuccessfulSendTime ??
            (pendingSince - MaximumPriorityAgingSeconds);
        double age = Math.Max(0d, currentTime - effectiveLastSent);
        double lastUpdatedComponent = Math.Pow(
            0.5d,
            age / StalenessHalfLifeSeconds);

        return new MovementPriorityKey(
            isLocalMainAgent ? 0 : 1,
            distanceComponent * lastUpdatedComponent,
            lastSuccessfulSendTime ?? float.MinValue,
            pendingSince,
            agentId);
    }

    public int Compare(MovementPriorityKey left, MovementPriorityKey right)
    {
        int result = left.Tier.CompareTo(right.Tier);
        if (result != 0) return result;

        result = left.Score.CompareTo(right.Score);
        if (result != 0) return result;

        result = left.LastSuccessfulSendTime.CompareTo(right.LastSuccessfulSendTime);
        if (result != 0) return result;

        result = left.PendingSince.CompareTo(right.PendingSince);
        if (result != 0) return result;

        return left.AgentId.CompareTo(right.AgentId);
    }
}
