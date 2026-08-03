using System;
using System.Collections.Generic;

namespace Missions.Agents.Handlers;

public interface IAgentActionPollScheduler
{
    int CurrentPartition { get; }
    int DirtyAgentCount { get; }
    IReadOnlyCollection<Guid> CurrentFallbackAgentIds { get; }
    IReadOnlyCollection<Guid> NewPriorityAgentIds { get; }

    bool RequiresAgentRefresh(long registryVersion);
    void BeginPoll(
        long registryVersion,
        IReadOnlyCollection<Guid> refreshedAgentIds);
    IReadOnlyCollection<Guid> GetDirtyAgentIds();
    bool TryBeginAgent(Guid agentId);
    void CompletePoll(Guid agentId, bool actionChanged);
    bool MarkDirty(Guid agentId);
    bool RemoveDirty(Guid agentId);
    void Clear();
}

internal sealed class AgentActionPollScheduler : IAgentActionPollScheduler
{
    internal const int FallbackPartitionCount = 4;
    internal const int CleanPriorityPolls = 2;

    private readonly List<Guid>[] fallbackPartitions;
    private readonly HashSet<Guid> knownAgentIds =
        new HashSet<Guid>();
    private readonly HashSet<Guid> refreshedAgentIdSet =
        new HashSet<Guid>();
    private readonly List<Guid> newPriorityAgentIds =
        new List<Guid>();
    private readonly Dictionary<Guid, DirtyAgentState> dirtyAgents =
        new Dictionary<Guid, DirtyAgentState>();
    private readonly List<Guid> dirtyAgentSnapshot = new List<Guid>();
    private readonly HashSet<Guid> currentPollAgentIds =
        new HashSet<Guid>();

    private long snapshotVersion = -1;

    public AgentActionPollScheduler()
    {
        fallbackPartitions = new List<Guid>[FallbackPartitionCount];
        for (int partition = 0;
             partition < FallbackPartitionCount;
             partition++)
        {
            fallbackPartitions[partition] = new List<Guid>();
        }
    }

    public int CurrentPartition { get; private set; } = -1;
    public int DirtyAgentCount => dirtyAgents.Count;
    public IReadOnlyCollection<Guid> CurrentFallbackAgentIds =>
        CurrentPartition < 0
            ? Array.Empty<Guid>()
            : fallbackPartitions[CurrentPartition];
    public IReadOnlyCollection<Guid> NewPriorityAgentIds =>
        newPriorityAgentIds;

    public bool RequiresAgentRefresh(long registryVersion) =>
        snapshotVersion < 0
        || (registryVersion != snapshotVersion
            && CurrentPartition == FallbackPartitionCount - 1);

    public void BeginPoll(
        long registryVersion,
        IReadOnlyCollection<Guid> refreshedAgentIds)
    {
        bool refreshAgents = RequiresAgentRefresh(registryVersion);
        newPriorityAgentIds.Clear();
        if (refreshAgents)
        {
            if (refreshedAgentIds == null)
                throw new ArgumentNullException(nameof(refreshedAgentIds));

            bool initialSnapshot = snapshotVersion < 0;
            foreach (List<Guid> partition in fallbackPartitions)
                partition.Clear();
            refreshedAgentIdSet.Clear();
            foreach (Guid agentId in refreshedAgentIds)
            {
                refreshedAgentIdSet.Add(agentId);
                fallbackPartitions[GetPartition(agentId)].Add(agentId);
                if (!initialSnapshot && !knownAgentIds.Contains(agentId))
                    newPriorityAgentIds.Add(agentId);
            }
            knownAgentIds.Clear();
            foreach (Guid agentId in refreshedAgentIdSet)
                knownAgentIds.Add(agentId);
            snapshotVersion = registryVersion;
        }
        else if (refreshedAgentIds != null)
        {
            throw new ArgumentException(
                "Agent ids were supplied when no refresh was due.",
                nameof(refreshedAgentIds));
        }

        currentPollAgentIds.Clear();
        CurrentPartition =
            (CurrentPartition + 1) % FallbackPartitionCount;
    }

    public IReadOnlyCollection<Guid> GetDirtyAgentIds()
    {
        dirtyAgentSnapshot.Clear();
        foreach (Guid agentId in dirtyAgents.Keys)
            dirtyAgentSnapshot.Add(agentId);
        return dirtyAgentSnapshot;
    }

    public bool TryBeginAgent(Guid agentId) =>
        currentPollAgentIds.Add(agentId);

    public void CompletePoll(Guid agentId, bool actionChanged)
    {
        if (actionChanged)
        {
            MarkDirty(agentId);
            return;
        }

        if (!dirtyAgents.TryGetValue(
                agentId,
                out DirtyAgentState state))
        {
            return;
        }

        state.CleanPollsRemaining--;
        if (state.CleanPollsRemaining <= 0)
        {
            dirtyAgents.Remove(agentId);
        }
        else
        {
            dirtyAgents[agentId] = state;
        }
    }

    public bool MarkDirty(Guid agentId)
    {
        bool added = !dirtyAgents.TryGetValue(
            agentId,
            out DirtyAgentState state);
        state.CleanPollsRemaining = CleanPriorityPolls;
        dirtyAgents[agentId] = state;
        return added;
    }

    public bool RemoveDirty(Guid agentId) =>
        dirtyAgents.Remove(agentId);

    public void Clear()
    {
        foreach (List<Guid> partition in fallbackPartitions)
            partition.Clear();
        knownAgentIds.Clear();
        refreshedAgentIdSet.Clear();
        newPriorityAgentIds.Clear();
        dirtyAgents.Clear();
        dirtyAgentSnapshot.Clear();
        currentPollAgentIds.Clear();
        snapshotVersion = -1;
        CurrentPartition = -1;
    }

    internal static int GetPartition(Guid agentId) =>
        (agentId.GetHashCode() & int.MaxValue)
        % FallbackPartitionCount;

    private struct DirtyAgentState
    {
        public int CleanPollsRemaining;
    }
}
