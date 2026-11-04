using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBreakIn : ICommand
{
    [ProtoMember(1)] public string RequestId { get; }
    [ProtoMember(2)] public uint PartyId { get; }
    [ProtoMember(3)] public uint SettlementId { get; }
    [ProtoMember(4)] public uint SiegeId { get; }

    public NetworkRequestBreakIn(string requestId, uint partyId, uint settlementId, uint siegeId)
    {
        RequestId = requestId;
        PartyId = partyId;
        SettlementId = settlementId;
        SiegeId = siegeId;
    }
}
