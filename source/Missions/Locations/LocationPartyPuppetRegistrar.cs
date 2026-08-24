using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Locations;

/// <summary>[Game thread] Spawns and registers a remote party puppet before returning.</summary>
public interface ILocationPartyPuppetRegistrar
{
    bool TrySpawnAndRegister(
        Func<Agent> spawnAgent,
        INetworkAgentRegistry agentRegistry,
        string controllerId,
        Guid agentId,
        out Agent agent);
}

/// <inheritdoc cref="ILocationPartyPuppetRegistrar"/>
public class LocationPartyPuppetRegistrar : ILocationPartyPuppetRegistrar
{
    public bool TrySpawnAndRegister(
        Func<Agent> spawnAgent,
        INetworkAgentRegistry agentRegistry,
        string controllerId,
        Guid agentId,
        out Agent agent)
    {
        agent = spawnAgent();
        return agent != null && agentRegistry.TryRegisterAgent(controllerId, agentId, agent);
    }
}
