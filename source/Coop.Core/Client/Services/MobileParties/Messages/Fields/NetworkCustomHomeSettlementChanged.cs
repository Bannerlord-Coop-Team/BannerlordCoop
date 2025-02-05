using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _customHomeSettlement
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCustomHomeSettlementChanged(string CustomHomeSettlementId, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public string CustomHomeSettlementId { get; } = CustomHomeSettlementId;

    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}