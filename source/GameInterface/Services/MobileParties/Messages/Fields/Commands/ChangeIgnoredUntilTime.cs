using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record ChangeIgnoredUntilTime(long IgnoredUntilTime, string MobilePartyId) : ICommand
{
    public long IgnoredUntilTime { get; } = IgnoredUntilTime;
    public string MobilePartyId { get; } = MobilePartyId;
}