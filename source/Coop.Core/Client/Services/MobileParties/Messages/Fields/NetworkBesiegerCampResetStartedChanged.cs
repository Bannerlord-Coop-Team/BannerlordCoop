using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBesiegerCampResetStartedChanged(bool BesiegerCampResetStarted, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public bool BesiegerCampResetStarted { get; } = BesiegerCampResetStarted;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}