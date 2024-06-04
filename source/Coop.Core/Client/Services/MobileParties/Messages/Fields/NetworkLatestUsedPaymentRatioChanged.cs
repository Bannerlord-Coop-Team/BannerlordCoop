using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _latestUsedPaymentRatio
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkLatestUsedPaymentRatioChanged(int LatestUsedPaymentRatio, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public int LatestUsedPaymentRatio { get; } = LatestUsedPaymentRatio;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}