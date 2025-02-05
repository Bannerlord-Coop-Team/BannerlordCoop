using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkIgnoredUntilTimeChanged(long IgnoredUntilTime, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public long IgnoredUntilTime { get; } = IgnoredUntilTime;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}