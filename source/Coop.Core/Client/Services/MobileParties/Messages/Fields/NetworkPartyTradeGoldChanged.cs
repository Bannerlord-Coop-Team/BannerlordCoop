using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPartyTradeGoldChanged(int PartyTradeGold, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public int PartyTradeGold { get; } = PartyTradeGold;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}