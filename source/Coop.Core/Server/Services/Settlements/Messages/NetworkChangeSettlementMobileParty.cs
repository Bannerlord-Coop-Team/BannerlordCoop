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
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }
    [ProtoMember(3)]
    public bool AddMobileParty { get; }

    public NetworkChangeSettlementMobileParty(string settlementId, string mobilePartyId, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        AddMobileParty = addMobileParty;
    }
}
