using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record ChangeAverageFleeTargetDirection(float VecX, float VecY, string MobilePartyId) : ICommand
{
    public float VecX { get; } = VecX;
    public float VecY { get; } = VecY;
    public string MobilePartyId { get; } = MobilePartyId;
}