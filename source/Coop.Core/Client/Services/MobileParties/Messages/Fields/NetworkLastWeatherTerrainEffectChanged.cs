using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkLastWeatherTerrainEffectChanged(int LastWeatherTerrainEffect, string MobilePartyId) : ICommand
{
    public int LastWeatherTerrainEffect { get; } = LastWeatherTerrainEffect;
    public string MobilePartyId { get; } = MobilePartyId;
}