using System;
using System.Collections.Concurrent;

namespace Missions.Locations;

/// <summary>
/// Per-mission identity set for player and companion agents received through party join info.
/// Identities remain until mission disposal so a delayed ambient spawn can never make a departed
/// companion eligible for settlement-host adoption.
/// </summary>
public interface ILocationPartyAgentMap
{
    void Record(Guid agentId);
    bool Contains(Guid agentId);
    bool ShouldAdoptAsNpc(Guid agentId, bool hasNpcBinding);
}

/// <inheritdoc cref="ILocationPartyAgentMap"/>
public class LocationPartyAgentMap : ILocationPartyAgentMap
{
    private readonly ConcurrentDictionary<Guid, byte> agentIds = new ConcurrentDictionary<Guid, byte>();

    public void Record(Guid agentId)
    {
        if (agentId != Guid.Empty)
            agentIds[agentId] = 0;
    }

    public bool Contains(Guid agentId)
        => agentId != Guid.Empty && agentIds.ContainsKey(agentId);

    public bool ShouldAdoptAsNpc(Guid agentId, bool hasNpcBinding)
        => hasNpcBinding && !Contains(agentId);
}
