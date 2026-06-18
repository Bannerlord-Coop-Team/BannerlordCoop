using Common.Logging;
using GameInterface.Missions.Agents.Messages;
using GameInterface.Missions.Agents.Packets;
using GameInterface.Services.Entity;
using LiteNetLib;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.DotNet;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions;

/// <summary>
/// Agent Registry for associating Agents over the network
/// </summary>
public interface INetworkAgentRegistry : IDisposable
{
    /// <summary>
    /// Clears all data
    /// </summary>
    void Clear();
}

/// <inheritdoc cref="INetworkAgentRegistry"/>
public class NetworkAgentRegistry : INetworkAgentRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<NetworkAgentRegistry>();
    private readonly IControllerIdProvider controllerIdProvider;

    private ConcurrentDictionary<string, ImmutableHashSet<string>> ControlledAgents = new();
    private ConditionalWeakTable<Agent, CoopAgentInfo> AgentToInfo = new();
    private ConcurrentDictionary<string, CoopAgentInfo> IdToInfo = new();

    public NetworkAgentRegistry(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    /// <inheritdoc/>
    public bool TryRegisterCoopAgent(string controllerId, string agentId, Agent agent, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(controllerId))
        {
            error = $"{nameof(controllerId)} is null or empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(agentId))
        {
            error = $"{nameof(agentId)} is null or empty.";
            return false;
        }

        if (agent == null)
        {
            error = $"{nameof(agent)} is null.";
            return false;
        }

        var agentInfo = new CoopAgentInfo(agent, agentId, controllerId);

        try
        {
            AgentToInfo.Add(agent, agentInfo);
            IdToInfo.TryAdd(agentId, agentInfo);
        }
        catch (ArgumentException)
        {
            error = $"Agent is already registered. ControllerId: {controllerId}, AgentId: {agentId}";
            Logger.Warning(error);
            return false;
        }

        var tempError = "";
        ControlledAgents.AddOrUpdate(
            controllerId,
            _ => ImmutableHashSet.Create(agentId),
            (_, existingAgents) =>
            {
                if (existingAgents.Contains(agentId))
                {
                    tempError = $"AgentId is already registered for controller. ControllerId: {controllerId}, AgentId: {agentId}";
                    return existingAgents;
                }

                return existingAgents.Add(agentId);
            });

        if (string.IsNullOrEmpty(tempError) == false)
        {
            error = tempError;
            AgentToInfo.Remove(agent);
            Logger.Warning(error);
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public bool RemoveCoopAgent(CoopAgentInfo agentInfo)
    {
        bool succeeded = false;
        ControlledAgents.AddOrUpdate(
            agentInfo.ControllerId,
            _ => ImmutableHashSet.Create(agentInfo.AgentId),
            (_, existingAgents) =>
            {
                if (!existingAgents.Contains(agentInfo.AgentId))
                {
                    return existingAgents;
                }

                succeeded = true;
                return existingAgents.Add(agentInfo.AgentId);
        });

        return succeeded;
    }

    /// <inheritdoc/>
    public bool TryGetAgentInfo(Agent agent, out CoopAgentInfo agentInfo)
    {
        return AgentToInfo.TryGetValue(agent, out agentInfo);
    }

    /// <inheritdoc/>
    public bool TryGetAgentInfo(string agentId, out CoopAgentInfo agentInfo)
    {
        return IdToInfo.TryGetValue(agentId, out agentInfo);
    }

    /// <inheritdoc/>
    public bool IsLocallyControlled(Agent agent)
    {
        if (agent == null) return false;

        if (!AgentToInfo.TryGetValue(agent, out var agentInfo))
            return false;

        return IsLocallyControlled(agentInfo);
    }

    /// <inheritdoc/>
    public bool IsLocallyControlled(string agentId)
    {
        if (string.IsNullOrEmpty(agentId)) return false;

        if (!IdToInfo.TryGetValue(agentId, out var agentInfo))
            return false;

        return IsLocallyControlled(agentInfo);
    }

    private bool IsLocallyControlled(CoopAgentInfo agentInfo)
    {
        if (agentInfo == null) return false;

        var localId = controllerIdProvider.ControllerId;
        return ControlledAgents.TryGetValue(localId, out var controlledIds)
            && controlledIds.Contains(agentInfo.AgentId);
    }


    /// <inheritdoc/>
    public void Clear()
    {
        ControlledAgents = new();
        AgentToInfo = new();
        IdToInfo = new();
    }

    /// <inheritdoc/>
    public bool TryGetExternalController(Agent agent, out string controllerId)
    {
        controllerId = null;

        if (!AgentToInfo.TryGetValue(agent, out var agentInfo))
            return false;

        controllerId = agentInfo.ControllerId;

        return true;
    }

    /// <inheritdoc/>
    public bool TryGetExternalController(string agentId, out string controllerId)
    {
        controllerId = null;

        if (!IdToInfo.TryGetValue(agentId, out var agentInfo))
            return false;

        controllerId = agentInfo.ControllerId;

        return true;
    }

    public void Dispose()
    {
        Clear();
    }
}

public class CoopAgentInfo
{
    public CoopAgentInfo(Agent agent, string controllerId, string agentId)
    {
        Agent = agent;
        ControllerId = controllerId;
        AgentId = agentId;
    }

    public Agent Agent { get; }
    public string ControllerId { get; }
    public string AgentId { get; }
}