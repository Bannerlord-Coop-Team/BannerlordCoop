using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentFormations : IEvent
{
    public const int MaxUpdates = 128;

    [ProtoMember(1)] public readonly string BattleInstanceId;
    [ProtoMember(2)] public readonly string ControllerId;
    [ProtoMember(3)] public readonly BattleAgentFormationData[] Agents;

    public NetworkBattleAgentFormations(string battleInstanceId, string controllerId, BattleAgentFormationData[] agents)
    {
        BattleInstanceId = battleInstanceId;
        ControllerId = controllerId;
        Agents = agents;
    }
}

[ProtoContract(SkipConstructor = true)]
public class BattleAgentFormationData
{
    [ProtoMember(1)] public readonly Guid AgentId;
    [ProtoMember(2)] public readonly int FormationIndex;
    [ProtoMember(3)] public readonly long AuthorityRevision;
    /// <summary>
    /// The hull whose crew the agent joined, or empty off any hull. Each client fields a hull into its own first free
    /// formation, so receivers place crew by this hull instead of the owner's formation index.
    /// </summary>
    [ProtoMember(4)] public readonly Guid ShipId;

    public BattleAgentFormationData(Guid agentId, int formationIndex, long authorityRevision, Guid shipId = default)
    {
        AgentId = agentId;
        FormationIndex = formationIndex;
        AuthorityRevision = authorityRevision;
        ShipId = shipId;
    }
}
