using Common.Messaging;
using Missions.Agents.Packets;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages;

public readonly struct LadderForkGranted : IEvent
{
    public Agent Agent { get; }

    public LadderForkGranted(Agent agent)
    {
        Agent = agent;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkLadderForkGranted : IEvent
{
    [ProtoMember(1)] public Guid GrantId { get; }
    [ProtoMember(2)] public Guid AgentId { get; }
    [ProtoMember(3)] public string Authority { get; }
    [ProtoMember(4)] public long AuthorityRevision { get; }
    [ProtoMember(5)] public string ItemObjectId { get; }
    [ProtoMember(6)] public short DataValue { get; }
    [ProtoMember(7)] public AgentEquipmentData Equipment { get; }

    public NetworkLadderForkGranted(Guid grantId, Guid agentId, string authority,
        long authorityRevision, string itemObjectId, short dataValue, AgentEquipmentData equipment)
    {
        GrantId = grantId;
        AgentId = agentId;
        Authority = authority;
        AuthorityRevision = authorityRevision;
        ItemObjectId = itemObjectId;
        DataValue = dataValue;
        Equipment = equipment;
    }
}
