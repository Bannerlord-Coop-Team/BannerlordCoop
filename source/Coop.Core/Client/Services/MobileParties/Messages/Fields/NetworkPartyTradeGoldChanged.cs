using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPartyTradeGoldChanged(int PartyTradeGold, string MobilePartyId) : ICommand
{
    public int PartyTradeGold { get; } = PartyTradeGold;
    public string MobilePartyId { get; } = MobilePartyId;
}