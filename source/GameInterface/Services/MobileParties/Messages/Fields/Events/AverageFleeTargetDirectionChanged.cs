using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record AverageFleeTargetDirectionChanged(float VecX, float VecY, string MobilePartyId) : IEvent
{
    public float VecX { get; } = VecX;
    public float VecY { get; } = VecY;
    public string MobilePartyId { get; } = MobilePartyId;
}