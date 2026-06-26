using Common.Logging;
using GameInterface.Services.Entity;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions;

/// <summary>
/// Associates mission <see cref="Agent"/>s with their networked id and the party they belong to.
/// </summary>
public interface INetworkAgentRegistry : IDisposable
{
    /// <summary>Clears all data.</summary>
    void Clear();
    bool TryRegisterAgent(string controllerId, Guid agentId, Agent agent);
    bool RemoveController(string controllerId);
    bool RemoveAgent(Guid agentId);
    bool RemoveAgent(Agent agent);
    bool TryGetAgentInfo(Agent agent, out CoopAgentInfo agentInfo);
    bool TryGetAgentInfo(Guid agentId, out CoopAgentInfo agentInfo);
    bool IsLocallyControlled(Guid agentId);
    bool IsLocallyControlled(Agent agent);
    bool TryTransferAuthority(string controllerId, Guid agentId);

    IReadOnlyCollection<CoopAgentInfo> GetAgents(string controllerId);
}

/// <inheritdoc cref="INetworkAgentRegistry"/>
public class NetworkAgentRegistry : INetworkAgentRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<NetworkAgentRegistry>();

    // Register / remove / clear are cold (spawn, death) so a single lock around every access keeps the
    // two views consistent with each other.
    private readonly object gate = new();
    private readonly Dictionary<Agent, CoopAgentInfo> AgentToInfo = new();
    private readonly Dictionary<Guid, CoopAgentInfo> IdToInfo = new();
    private readonly Dictionary<string, List<CoopAgentInfo>> ControllerAgentMap = new();
    private readonly IControllerIdProvider controllerIdProvider;

    public NetworkAgentRegistry(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public void Dispose() => Clear();

    /// <inheritdoc/>
    public void Clear()
    {
        lock (gate)
        {
            AgentToInfo.Clear();
            IdToInfo.Clear();
            ControllerAgentMap.Clear();
        }
    }

    /// <inheritdoc/>
    public bool TryRegisterAgent(string controllerId, Guid agentId, Agent agent)
    {
        if (string.IsNullOrEmpty(controllerId))
        {
            Logger.Error($"{nameof(controllerId)} is null or empty.");
            return false;
        }

        if (agentId == Guid.Empty)
        {
            Logger.Error($"{nameof(agentId)} is empty.");
            return false;
        }

        if (agent == null)
        {
            Logger.Error($"{nameof(agent)} is null.");
            return false;
        }

        var agentInfo = new CoopAgentInfo(controllerId, agent, agentId);

        lock (gate)
        {
            if (AgentToInfo.ContainsKey(agent) || IdToInfo.ContainsKey(agentId))
            {
                Logger.Error($"Agent is already registered. AgentId: {agentId}");
                return false;
            }

            AgentToInfo[agent] = agentInfo;
            IdToInfo[agentId] = agentInfo;

            if (!ControllerAgentMap.TryGetValue(agentInfo.CurrentAuthority, out var controlledAgents))
            {
                controlledAgents = new();
                ControllerAgentMap[agentInfo.CurrentAuthority] = controlledAgents;
            }

            controlledAgents.Add(agentInfo);

            return true;
        }
    }

    public bool RemoveAgent(Agent agent)
    {
        lock (gate)
        {
            if (!AgentToInfo.TryGetValue(agent, out var stored))
                return false;

            return RemoveAgentInternal(stored);
        }
    }

    /// <inheritdoc/>
    public bool RemoveAgent(Guid agentId)
    {
        lock (gate)
        {
            if (!IdToInfo.TryGetValue(agentId, out var stored))
                return false;

            return RemoveAgentInternal(stored);
        }
    }

    private bool RemoveAgentInternal(CoopAgentInfo agentInfo)
    {
        if (!ControllerAgentMap.TryGetValue(agentInfo.CurrentAuthority, out var controlledAgents))
        {
            Logger.Warning("Failed to remove agent. ControllerId {ControllerId}, AgentId {AgentId}");
            return false;
        }

        var succeeded = true;

        succeeded &= controlledAgents.Remove(agentInfo);
        succeeded &= IdToInfo.Remove(agentInfo.AgentId);
        succeeded &= AgentToInfo.Remove(agentInfo.Agent);
        return succeeded;
    }

    /// <inheritdoc/>
    public bool TryGetAgentInfo(Agent agent, out CoopAgentInfo agentInfo)
    {
        agentInfo = default;
        if (agent == null) return false;

        lock (gate)
        {
            return AgentToInfo.TryGetValue(agent, out agentInfo);
        }
    }

    /// <inheritdoc/>
    public bool TryGetAgentInfo(Guid agentId, out CoopAgentInfo agentInfo)
    {
        lock (gate)
        {
            return IdToInfo.TryGetValue(agentId, out agentInfo);
        }
    }

    public bool IsLocallyControlled(Guid agentId)
    {
        if (!IdToInfo.TryGetValue(agentId, out var stored))
            return false;

        return stored.CurrentAuthority == controllerIdProvider.ControllerId;
    }

    public bool IsLocallyControlled(Agent agent)
    {
        if (!AgentToInfo.TryGetValue(agent, out var stored))
            return false;

        return stored.CurrentAuthority == controllerIdProvider.ControllerId;
    }

    public bool RemoveController(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId))
        {
            Logger.Error($"{nameof(controllerId)} is null or empty.");
            return false;
        }

        lock (gate)
        {
            if (!ControllerAgentMap.TryGetValue(controllerId, out var controlledAgents))
                return false;

            foreach (var agentInfo in controlledAgents)
            {
                IdToInfo.Remove(agentInfo.AgentId);
                AgentToInfo.Remove(agentInfo.Agent);
            }

            return ControllerAgentMap.Remove(controllerId);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<CoopAgentInfo> GetAgents(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId))
        {
            Logger.Error($"{nameof(controllerId)} is null or empty.");
            return Array.Empty<CoopAgentInfo>();
        }

        lock (gate)
        {
            if (!ControllerAgentMap.TryGetValue(controllerId, out var agents))
                return Array.Empty<CoopAgentInfo>();

            // Snapshot under the lock — the underlying list can be mutated by register/remove.
            return agents.ToArray();
        }
    }

    /// <inheritdoc/>
    public bool TryTransferAuthority(string controllerId, Guid agentId)
    {
        if (string.IsNullOrEmpty(controllerId))
        {
            Logger.Error($"{nameof(controllerId)} is null or empty.");
            return false;
        }

        if (agentId == Guid.Empty)
        {
            Logger.Error($"{nameof(agentId)} is empty.");
            return false;
        }

        lock (gate)
        {
            if (!IdToInfo.TryGetValue(agentId, out var agentInfo))
            {
                Logger.Warning("Failed to transfer authority: agent {AgentId} is not registered.", agentId);
                return false;
            }

            // Idempotent — the target already holds authority, nothing to move.
            if (agentInfo.CurrentAuthority == controllerId)
                return true;

            // Detach from the current authority's list, pruning the entry when it empties so the map
            // stays consistent with RemoveController (which drops the whole key).
            if (ControllerAgentMap.TryGetValue(agentInfo.CurrentAuthority, out var currentAgents))
            {
                currentAgents.Remove(agentInfo);
                if (currentAgents.Count == 0)
                    ControllerAgentMap.Remove(agentInfo.CurrentAuthority);
            }

            agentInfo.CurrentAuthority = controllerId;

            if (!ControllerAgentMap.TryGetValue(controllerId, out var newAgents))
            {
                newAgents = new();
                ControllerAgentMap[controllerId] = newAgents;
            }

            newAgents.Add(agentInfo);

            return true;
        }
    }
}

public class CoopAgentInfo
{
    public Agent Agent { get; }
    public Guid AgentId { get; }
    public string OriginalOwner { get; }
    public string CurrentAuthority { get; internal set; }

    internal CoopAgentInfo(string ownerId, Agent agent, Guid agentId)
    {
        OriginalOwner = ownerId;
        CurrentAuthority = ownerId;
        Agent = agent;
        AgentId = agentId;
    }
}
