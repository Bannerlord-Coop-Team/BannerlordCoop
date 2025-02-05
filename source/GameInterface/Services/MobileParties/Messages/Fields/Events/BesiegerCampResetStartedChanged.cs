using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record BesiegerCampResetStartedChanged(bool BesiegerCampResetStarted, string MobilePartyId) : IEvent
{
    public bool BesiegerCampResetStarted { get; } = BesiegerCampResetStarted;
    public string MobilePartyId { get; } = MobilePartyId;
}