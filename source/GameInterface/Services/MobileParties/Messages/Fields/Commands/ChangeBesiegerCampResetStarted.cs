using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record ChangeBesiegerCampResetStarted(bool BesiegerCampResetStarted, string MobilePartyId) : ICommand
{
    public bool BesiegerCampResetStarted { get; } = BesiegerCampResetStarted;
    public string MobilePartyId { get; } = MobilePartyId;
}