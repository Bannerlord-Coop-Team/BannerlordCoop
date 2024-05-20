using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPartyComponentChanged(string PartyComponentId, string MobilePartyId) : ICommand
{
    public string PartyComponentId { get; } = PartyComponentId;
    public string MobilePartyId { get; } = MobilePartyId;
}