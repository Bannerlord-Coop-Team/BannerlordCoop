using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _customHomeSettlement
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCustomHomeSettlementChanged(string CustomHomeSettlementId, string MobilePartyId) : ICommand
{
    public string CustomHomeSettlementId { get; } = CustomHomeSettlementId;
    public string MobilePartyId { get; } = MobilePartyId;
}