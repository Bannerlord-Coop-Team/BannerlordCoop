using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify clients about a mobileparty change.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementMobileParty : IEvent
{
    [ProtoMember(1)]
    public uint SettlementId { get; }
    [ProtoMember(2)]
    public uint MobilePartyId { get; }
    [ProtoMember(3)]
    public bool AddMobileParty { get; }

    public NetworkChangeSettlementMobileParty(uint settlementId, uint mobilePartyId, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        AddMobileParty = addMobileParty;
    }
}
