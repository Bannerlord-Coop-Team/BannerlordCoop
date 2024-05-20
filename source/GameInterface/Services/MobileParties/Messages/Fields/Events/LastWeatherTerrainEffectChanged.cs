using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record LastWeatherTerrainEffectChanged(int LastWeatherTerrainEffect, string MobilePartyId) : IEvent
{
    public int LastWeatherTerrainEffect { get; } = LastWeatherTerrainEffect;
    public string MobilePartyId { get; } = MobilePartyId;
}