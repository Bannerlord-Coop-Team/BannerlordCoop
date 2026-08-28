using Common.Logging;
using GameInterface.Services.Entity;
using Missions.Agents.Packets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
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
    bool TryRegisterAgent(
        string controllerId,
        Guid agentId,
        ushort movementId,
        Agent agent,
        long authorityRevision = 0);
    bool TryRegisterAgent(string controllerId, string originalOwner, Guid agentId, ushort movementId, Agent agent);
    bool TryRegisterAgent(
        string controllerId,
        string originalOwner,
        string movementScopeId,
        Guid agentId,
        ushort movementId,
        Agent agent,
        long authorityRevision = 0);
    bool RemoveController(string controllerId);
    bool RemoveAgent(Guid agentId);
    bool RemoveAgent(Agent agent);
    bool TryGetAgentInfo(Agent agent, out CoopAgentInfo agentInfo);
    bool TryGetAgentInfo(Guid agentId, out CoopAgentInfo agentInfo);
    bool TryGetAgentInfo(string movementScopeId, ushort movementId, out CoopAgentInfo agentInfo);
    bool TryGetHeroAgentInfo(Hero hero, out CoopAgentInfo agentInfo);
    bool IsLocallyControlled(Guid agentId);
    bool IsLocallyControlled(Agent agent);
    bool TryTransferAuthority(string controllerId, Guid agentId);
    bool TryTransferAuthority(string controllerId, Guid agentId, long authorityRevision);

    IReadOnlyCollection<CoopAgentInfo> GetAgents(string controllerId);

    /// <summary>
    /// Every controller id that currently holds at least one registered agent (by CURRENT authority, the
    /// same keying as <see cref="GetAgents"/>). Snapshot — safe to iterate while the registry mutates.
    /// </summary>
    IReadOnlyCollection<string> GetControllerIds();
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
    private readonly Dictionary<(string Scope, ushort MovementId), CoopAgentInfo> MovementIdToInfo = new();
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
            MovementIdToInfo.Clear();
            ControllerAgentMap.Clear();
        }
    }

    /// <inheritdoc/>
    public bool TryRegisterAgent(string controllerId, Guid agentId, Agent agent)
    {
        return TryRegisterAgent(
            controllerId, controllerId, controllerId, agentId, 0, agent, 0);
    }

    /// <inheritdoc/>
    public bool TryRegisterAgent(
        string controllerId,
        Guid agentId,
        ushort movementId,
        Agent agent,
        long authorityRevision = 0)
    {
        return TryRegisterAgent(
            controllerId,
            controllerId,
            controllerId,
            agentId,
            movementId,
            agent,
            authorityRevision);
    }

    /// <inheritdoc/>
    public bool TryRegisterAgent(
        string controllerId,
        string originalOwner,
        Guid agentId,
        ushort movementId,
        Agent agent)
    {
        return TryRegisterAgent(
            controllerId,
            originalOwner,
            originalOwner,
            agentId,
            movementId,
            agent,
            0);
    }

    /// <inheritdoc/>
    public bool TryRegisterAgent(
        string controllerId,
        string originalOwner,
        string movementScopeId,
        Guid agentId,
        ushort movementId,
        Agent agent,
        long authorityRevision = 0)
    {
        if (string.IsNullOrEmpty(controllerId))
        {
            Logger.Error($"{nameof(controllerId)} is null or empty.");
            return false;
        }

        if (string.IsNullOrEmpty(originalOwner))
        {
            Logger.Error($"{nameof(originalOwner)} is null or empty.");
            return false;
        }

        if (string.IsNullOrEmpty(movementScopeId))
        {
            Logger.Error($"{nameof(movementScopeId)} is null or empty.");
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

        if (authorityRevision < 0)
        {
            Logger.Error($"{nameof(authorityRevision)} is negative.");
            return false;
        }

        var agentInfo = new CoopAgentInfo(
            controllerId,
            originalOwner,
            movementScopeId,
            agent,
            agentId,
            movementId,
            authorityRevision);

        lock (gate)
        {
            bool nativeAgentCollision = AgentToInfo.TryGetValue(agent, out var existingByAgent);
            bool canonicalIdCollision = IdToInfo.TryGetValue(agentId, out var existingById);
            CoopAgentInfo existingByMovement = null;
            bool movementIdCollision = movementId != 0 &&
                MovementIdToInfo.TryGetValue((movementScopeId, movementId), out existingByMovement);
            if (nativeAgentCollision || canonicalIdCollision || movementIdCollision)
            {
                CoopAgentInfo existing = existingByAgent ?? existingById ?? existingByMovement;
                Logger.Error(
                    "[AgentRegistry] Rejected agent registration: controller={ControllerId} " +
                    "originalOwner={OriginalOwner} agentId={AgentId} movementIdentity={Scope}/{MovementId} " +
                    "agentIndex={AgentIndex} nativeAgentCollision={NativeAgentCollision} " +
                    "canonicalIdCollision={CanonicalIdCollision} movementIdCollision={MovementIdCollision} " +
                    "existingAgentId={ExistingAgentId} existingAuthority={ExistingAuthority} " +
                    "existingMovementIdentity={ExistingScope}/{ExistingMovementId} existingAgentIndex={ExistingAgentIndex}",
                    controllerId,
                    originalOwner,
                    agentId,
                    movementScopeId,
                    movementId,
                    agent.Index,
                    nativeAgentCollision,
                    canonicalIdCollision,
                    movementIdCollision,
                    existing?.AgentId ?? Guid.Empty,
                    existing?.CurrentAuthority,
                    existing?.MovementScopeId,
                    existing?.MovementId ?? 0,
                    existing?.Agent?.Index ?? -1);
                return false;
            }

            AgentToInfo[agent] = agentInfo;
            IdToInfo[agentId] = agentInfo;
            if (movementId != 0)
                MovementIdToInfo[(movementScopeId, movementId)] = agentInfo;

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
        if (agentInfo.MovementId != 0)
            succeeded &= MovementIdToInfo.Remove((agentInfo.MovementScopeId, agentInfo.MovementId));
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

    /// <inheritdoc/>
    public bool TryGetAgentInfo(string movementScopeId, ushort movementId, out CoopAgentInfo agentInfo)
    {
        if (string.IsNullOrEmpty(movementScopeId) || movementId == 0)
        {
            agentInfo = default;
            return false;
        }

        lock (gate)
        {
            return MovementIdToInfo.TryGetValue((movementScopeId, movementId), out agentInfo);
        }
    }

    /// <inheritdoc/>
    public bool TryGetHeroAgentInfo(Hero hero, out CoopAgentInfo agentInfo)
    {
        agentInfo = default;
        if (hero == null) return false;

        lock (gate)
        {
            foreach (var info in AgentToInfo.Values)
            {
                // Skip this agent if not the target hero
                if (info.Agent.Character is not CharacterObject character
                    || !character.IsHero
                    || character.HeroObject != hero)
                {
                    continue;
                }

                agentInfo = info;
                return true;
            }
        }

        return false;
    }

    public bool IsLocallyControlled(Guid agentId)
    {
        lock (gate)
        {
            if (!IdToInfo.TryGetValue(agentId, out var stored))
                return false;

            return stored.CurrentAuthority == controllerIdProvider.ControllerId;
        }
    }

    public bool IsLocallyControlled(Agent agent)
    {
        lock (gate)
        {
            if (!AgentToInfo.TryGetValue(agent, out var stored))
                return false;

            return stored.CurrentAuthority == controllerIdProvider.ControllerId;
        }
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
                if (agentInfo.MovementId != 0)
                    MovementIdToInfo.Remove((agentInfo.MovementScopeId, agentInfo.MovementId));
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
    public IReadOnlyCollection<string> GetControllerIds()
    {
        lock (gate)
        {
            // Snapshot under the lock — the map can be mutated by register/remove/transfer.
            return ControllerAgentMap.Keys.ToArray();
        }
    }

    /// <inheritdoc/>
    public bool TryTransferAuthority(string controllerId, Guid agentId)
        => TryTransferAuthority(controllerId, agentId, authorityRevision: null);

    /// <inheritdoc/>
    public bool TryTransferAuthority(string controllerId, Guid agentId, long authorityRevision)
        => TryTransferAuthority(controllerId, agentId, (long?)authorityRevision);

    private bool TryTransferAuthority(string controllerId, Guid agentId, long? authorityRevision)
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

        if (authorityRevision < 0)
        {
            Logger.Error($"{nameof(authorityRevision)} is negative.");
            return false;
        }

        lock (gate)
        {
            if (!IdToInfo.TryGetValue(agentId, out var agentInfo))
            {
                Logger.Warning("Failed to transfer authority: agent {AgentId} is not registered.", agentId);
                return false;
            }

            if (authorityRevision < agentInfo.AuthorityRevision)
            {
                Logger.Warning(
                    "Failed to transfer authority for agent {AgentId}: revision {Revision} is older than {CurrentRevision}.",
                    agentId,
                    authorityRevision,
                    agentInfo.AuthorityRevision);
                return false;
            }

            if (agentInfo.CurrentAuthority == controllerId)
            {
                if (authorityRevision > agentInfo.AuthorityRevision)
                {
                    agentInfo.AuthorityRevision = authorityRevision.Value;
                    agentInfo.ClearAuthoritativeEquipment();
                }
                return true;
            }

            // Detach from the current authority's list, pruning the entry when it empties so the map
            // stays consistent with RemoveController (which drops the whole key).
            if (ControllerAgentMap.TryGetValue(agentInfo.CurrentAuthority, out var currentAgents))
            {
                currentAgents.Remove(agentInfo);
                if (currentAgents.Count == 0)
                    ControllerAgentMap.Remove(agentInfo.CurrentAuthority);
            }

            agentInfo.CurrentAuthority = controllerId;
            agentInfo.AuthorityRevision = authorityRevision ?? agentInfo.AuthorityRevision + 1;
            agentInfo.ClearAuthoritativeEquipment();

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
    private AgentEquipmentData authoritativeEquipment;
    private bool hasAuthoritativeEquipment;

    public Agent Agent { get; }
    public Guid AgentId { get; }
    public ushort MovementId { get; }
    public string OriginalOwner { get; }
    public string MovementScopeId { get; }
    public string CurrentAuthority { get; internal set; }
    public long AuthorityRevision { get; internal set; }

    internal void RecordAuthoritativeEquipment(AgentEquipmentData equipment)
    {
        authoritativeEquipment = equipment;
        hasAuthoritativeEquipment = true;
    }

    internal bool TryGetAuthoritativeEquipment(out AgentEquipmentData equipment)
    {
        equipment = authoritativeEquipment;
        return hasAuthoritativeEquipment;
    }

    internal void ClearAuthoritativeEquipment()
    {
        authoritativeEquipment = default;
        hasAuthoritativeEquipment = false;
    }

    internal CoopAgentInfo(
        string currentAuthority,
        string originalOwner,
        string movementScopeId,
        Agent agent,
        Guid agentId,
        ushort movementId,
        long authorityRevision = 0)
    {
        OriginalOwner = originalOwner;
        MovementScopeId = movementScopeId;
        CurrentAuthority = currentAuthority;
        Agent = agent;
        AgentId = agentId;
        MovementId = movementId;
        AuthorityRevision = authorityRevision;
    }
}
