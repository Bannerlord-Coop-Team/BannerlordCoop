using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to make an army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerCreatedArmy : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;
    [ProtoMember(2)]
    public readonly string LeaderId;
    [ProtoMember(3)]
    public readonly string TargetSettlementId;
    [ProtoMember(4)]
    public readonly string ArmyTypeId;
    [ProtoMember(5)]
    public readonly List<string> PartyIds;

    public NetworkPlayerCreatedArmy(string kingdomId, string leaderId, string targetSettlementId,  string armyTypeId, List<string> partyIds)
    {
        KingdomId = kingdomId;
        LeaderId = leaderId;
        TargetSettlementId = targetSettlementId;
        ArmyTypeId = armyTypeId;
        PartyIds = partyIds;
    }
}
