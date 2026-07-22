using System;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>
/// Per-battle aliases from a superseded spawn id to the current id for the same stable troop. Only terminal
/// messages use these aliases: movement and actions from an old authority must never drive the replacement.
/// </summary>
public interface IBattleAgentIdAliasMap
{
    void Record(Guid supersededId, Guid currentId);
    bool TryResolve(Guid agentId, out Guid currentId);
    void Clear();
}

/// <inheritdoc cref="IBattleAgentIdAliasMap"/>
public class BattleAgentIdAliasMap : IBattleAgentIdAliasMap
{
    private readonly Dictionary<Guid, Guid> aliases = new Dictionary<Guid, Guid>();

    public void Record(Guid supersededId, Guid currentId)
    {
        if (supersededId == Guid.Empty || currentId == Guid.Empty || supersededId == currentId)
            return;

        if (TryResolve(currentId, out var resolvedCurrentId))
            currentId = resolvedCurrentId;

        aliases[supersededId] = currentId;
    }

    public bool TryResolve(Guid agentId, out Guid currentId)
    {
        currentId = agentId;
        var visited = new HashSet<Guid>();
        while (aliases.TryGetValue(currentId, out var nextId))
        {
            if (!visited.Add(currentId) || nextId == Guid.Empty)
            {
                currentId = agentId;
                return false;
            }

            currentId = nextId;
        }

        return currentId != agentId;
    }

    public void Clear() => aliases.Clear();
}
