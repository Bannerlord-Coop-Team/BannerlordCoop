using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// 
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSetWagePaymentLimitApproved : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public int WageAmount { get; }

    public NetworkSetWagePaymentLimitApproved(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
