using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBreakOut : ICommand
{
    [ProtoMember(1)] public string RequestId { get; }
    [ProtoMember(2)] public uint PartyId { get; }
    [ProtoMember(3)] public uint SettlementId { get; }
    [ProtoMember(4)] public uint SiegeId { get; }
    [ProtoMember(5)] public bool IsFromPort { get; }

    public NetworkRequestBreakOut(string requestId, uint partyId, uint settlementId, uint siegeId, bool isFromPort)
    {
        RequestId = requestId;
        PartyId = partyId;
        SettlementId = settlementId;
        SiegeId = siegeId;
        IsFromPort = isFromPort;
    }
}
