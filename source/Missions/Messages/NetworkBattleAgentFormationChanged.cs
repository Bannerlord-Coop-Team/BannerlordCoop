using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>Reliable owner-to-peers update of an existing battle agent's formation slot (-1 means none).</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentFormationChanged : IEvent
{
    [ProtoMember(1)]
    public readonly string BattleInstanceId;
    [ProtoMember(2)]
    public readonly Guid AgentId;
    [ProtoMember(3)]
    public readonly string ControllerId;
    [ProtoMember(4)]
    public readonly long AuthorityRevision;
    [ProtoMember(5)]
    public readonly int FormationIndex;

    public NetworkBattleAgentFormationChanged(
        string battleInstanceId, Guid agentId, string controllerId, long authorityRevision, int formationIndex)
    {
        BattleInstanceId = battleInstanceId;
        AgentId = agentId;
        ControllerId = controllerId;
        AuthorityRevision = authorityRevision;
        FormationIndex = formationIndex;
    }
}
