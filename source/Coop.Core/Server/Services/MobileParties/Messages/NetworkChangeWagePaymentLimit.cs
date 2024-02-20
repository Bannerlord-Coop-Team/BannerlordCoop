using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Sent to all other clients then the requestor of changing pay limit
/// </summary>
public record NetworkChangeWagePaymentLimit : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public int WageAmount { get; }

    public NetworkChangeWagePaymentLimit(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
