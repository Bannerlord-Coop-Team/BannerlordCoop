using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct AssignClanMembersToSettlement : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly List<string> HeroIds;

    public AssignClanMembersToSettlement(string settlementId, string mainPartyId, List<string> heroIds)
    {
        SettlementId = settlementId;
        MainPartyId = mainPartyId;
        HeroIds = heroIds;
    }
}
