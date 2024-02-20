using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify clients about a mobileparty change.
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkChangeSettlementMobileParty : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }
    [ProtoMember(3)]
    public int NumberOfLordParties { get; }
    [ProtoMember(4)]
    public bool AddMobileParty { get; }

    public NetworkChangeSettlementMobileParty(string settlementId, string mobilePartyId, int numberOfLordParties, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        NumberOfLordParties = numberOfLordParties;
        AddMobileParty = addMobileParty;
    }
}
