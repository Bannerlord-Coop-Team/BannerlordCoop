using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Request the server to change paymentlimit request
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeWagePaymentLimitRequest : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public int WageAmount { get; }

    public NetworkChangeWagePaymentLimitRequest(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
