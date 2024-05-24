using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record ChangeLastWeatherTerrainEffect(int LastWeatherTerrainEffect, string MobilePartyId) : ICommand
{
    public int LastWeatherTerrainEffect { get; } = LastWeatherTerrainEffect;
    public string MobilePartyId { get; } = MobilePartyId;
}