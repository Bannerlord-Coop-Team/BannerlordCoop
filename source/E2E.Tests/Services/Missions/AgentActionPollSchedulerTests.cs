using Missions.Agents.Handlers;
using System;
using System.Collections.Generic;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class AgentActionPollSchedulerTests
{
    [Fact]
    public void FallbackPartitions_VisitEveryAgentWithinOneSweep()
    {
        var scheduler = new AgentActionPollScheduler();
        var agentIds = new List<Guid>();
        for (int partition = 0;
             partition < AgentActionPollScheduler.FallbackPartitionCount;
             partition++)
        {
            agentIds.Add(CreateAgentId(partition, 0));
            agentIds.Add(CreateAgentId(partition, 1));
        }

        var visits = new Dictionary<Guid, int>();
        for (int poll = 0;
             poll < AgentActionPollScheduler.FallbackPartitionCount;
             poll++)
        {
            scheduler.BeginPoll(
                registryVersion: 1,
                refreshedAgentIds: poll == 0 ? agentIds : null);
            foreach (Guid agentId in
                     scheduler.CurrentFallbackAgentIds)
            {
                visits.TryGetValue(agentId, out int count);
                visits[agentId] = count + 1;
            }
        }

        Assert.Equal(agentIds.Count, visits.Count);
        Assert.All(visits.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public void ChangedRegistry_RefreshesAtTheBoundaryAndPrioritizesNewAgents()
    {
        var scheduler = new AgentActionPollScheduler();
        Guid existingAgentId = CreateAgentId(partition: 0, index: 0);
        Guid newAgentId = CreateAgentId(partition: 3, index: 0);
        var initialAgentIds = new[] { existingAgentId };

        scheduler.BeginPoll(
            registryVersion: 1,
            refreshedAgentIds: initialAgentIds);
        Assert.Empty(scheduler.NewPriorityAgentIds);

        for (int poll = 0; poll < 3; poll++)
        {
            Assert.False(scheduler.RequiresAgentRefresh(
                registryVersion: 2));
            scheduler.BeginPoll(
                registryVersion: 2,
                refreshedAgentIds: null);
            Assert.Empty(scheduler.NewPriorityAgentIds);
        }

        Assert.True(scheduler.RequiresAgentRefresh(
            registryVersion: 2));
        scheduler.BeginPoll(
            registryVersion: 2,
            refreshedAgentIds: new[]
            {
                existingAgentId,
                newAgentId,
            });

        Assert.Equal(
            newAgentId,
            Assert.Single(scheduler.NewPriorityAgentIds));
    }

    [Fact]
    public void DirtyAgent_RemainsPriorityForTwoCleanPolls()
    {
        var scheduler = new AgentActionPollScheduler();
        Guid agentId = CreateAgentId(partition: 3, index: 0);
        Assert.True(scheduler.MarkDirty(agentId));

        for (int poll = 0;
             poll < AgentActionPollScheduler.CleanPriorityPolls;
             poll++)
        {
            scheduler.BeginPoll(
                registryVersion: 1,
                refreshedAgentIds: poll == 0
                    ? new[] { agentId }
                    : null);
            Assert.Contains(agentId, scheduler.GetDirtyAgentIds());
            scheduler.CompletePoll(agentId, actionChanged: false);
        }

        Assert.Equal(0, scheduler.DirtyAgentCount);
        Assert.Empty(scheduler.GetDirtyAgentIds());
    }

    [Fact]
    public void ActionChange_RefreshesTheCleanPollWindow()
    {
        var scheduler = new AgentActionPollScheduler();
        Guid agentId = CreateAgentId(partition: 3, index: 0);
        scheduler.MarkDirty(agentId);

        scheduler.BeginPoll(
            registryVersion: 1,
            refreshedAgentIds: new[] { agentId });
        scheduler.CompletePoll(agentId, actionChanged: false);

        scheduler.BeginPoll(
            registryVersion: 1,
            refreshedAgentIds: null);
        scheduler.CompletePoll(agentId, actionChanged: true);

        Assert.Equal(1, scheduler.DirtyAgentCount);
        for (int poll = 0;
             poll < AgentActionPollScheduler.CleanPriorityPolls;
             poll++)
        {
            scheduler.BeginPoll(
                registryVersion: 1,
                refreshedAgentIds: null);
            Assert.Contains(agentId, scheduler.GetDirtyAgentIds());
            scheduler.CompletePoll(agentId, actionChanged: false);
        }
        Assert.Equal(0, scheduler.DirtyAgentCount);
    }

    [Fact]
    public void RemovedDirtyAgent_IsNoLongerPriority()
    {
        var scheduler = new AgentActionPollScheduler();
        Guid agentId = Guid.NewGuid();
        scheduler.MarkDirty(agentId);

        Assert.True(scheduler.RemoveDirty(agentId));

        Assert.Empty(scheduler.GetDirtyAgentIds());
    }

    [Fact]
    public void TryBeginAgent_SuppressesDuplicatePriorityAndFallbackVisits()
    {
        var scheduler = new AgentActionPollScheduler();
        Guid agentId = CreateAgentId(partition: 0, index: 0);
        scheduler.BeginPoll(
            registryVersion: 1,
            refreshedAgentIds: new[] { agentId });

        Assert.True(scheduler.TryBeginAgent(agentId));
        Assert.False(scheduler.TryBeginAgent(agentId));
    }

    [Fact]
    public void Clear_RestartsAtTheFirstPartition()
    {
        var scheduler = new AgentActionPollScheduler();
        scheduler.MarkDirty(Guid.NewGuid());
        scheduler.BeginPoll(
            registryVersion: 1,
            refreshedAgentIds: Array.Empty<Guid>());

        scheduler.Clear();
        scheduler.BeginPoll(
            registryVersion: 1,
            refreshedAgentIds: Array.Empty<Guid>());

        Assert.Equal(0, scheduler.CurrentPartition);
        Assert.Equal(0, scheduler.DirtyAgentCount);
    }

    private static Guid CreateAgentId(int partition, int index)
    {
        for (int value = 1; ; value++)
        {
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(
                BitConverter.GetBytes(value),
                0,
                bytes,
                0,
                sizeof(int));
            Buffer.BlockCopy(
                BitConverter.GetBytes(index),
                0,
                bytes,
                sizeof(int),
                sizeof(int));
            var agentId = new Guid(bytes);
            if (AgentActionPollScheduler.GetPartition(agentId)
                == partition)
            {
                return agentId;
            }
        }
    }
}
