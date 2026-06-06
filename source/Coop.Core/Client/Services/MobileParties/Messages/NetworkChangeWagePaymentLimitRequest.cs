using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Request the server to change paymentlimit request
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeWagePaymentLimitRequest : IEvent
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;
    [ProtoMember(2)]
    public readonly int WageAmount;

    public NetworkChangeWagePaymentLimitRequest(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
