using Autofac.Features.OwnedInstances;
using Common;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.Heroes.Data;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Data;

/// <summary>
/// Represents the current state of the game that the game transfer
/// couldn't handle
/// </summary>
public interface ICoopSession
{
    string UniqueGameId { get; }
    Dictionary<string, HashSet<ControlledEntity>> ControlledEntityMap { get; }
}

/// <inheritdoc cref="ICoopSession"/>
[ProtoContract]
public class CoopSession : ICoopSession
{
    [ProtoMember(1)]
    public string UniqueGameId { get; }
    [ProtoMember(2)]
    public Dictionary<string, HashSet<ControlledEntity>> ControlledEntityMap { get; }

    public CoopSession(string uniqueGameId, Dictionary<string, HashSet<ControlledEntity>>  controlledEntityMap)
    {
        UniqueGameId = uniqueGameId;
        ControlledEntityMap = controlledEntityMap;
    }

    public override bool Equals(object obj)
    {
        if (obj is CoopSession session == false) return false;

        if (UniqueGameId != session.UniqueGameId) return false;

        if (ControlledEntityMap.Count != session.ControlledEntityMap.Count) return false;

        if (ControlledEntityMap.Zip(session.ControlledEntityMap, (l, r) =>
        {
            return l.Key == r.Key && l.Value.SetEquals(r.Value);
        }).All(x => x) == false) return false;

        return true;
    }

    public override int GetHashCode()
    {
        int hash = 1236898;
        hash = (hash * 31) + UniqueGameId.GetHashCode();
        hash = (hash * 31) + ControlledEntityMap.GetHashCode();
        return hash;
    }
}
