using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Updates the server of new Settlement.NumberOfEnemiesSpottedAround.
/// </summary>
public record SettlementChangedEnemiesSpotted : ICommand
{
    public string SettlementId { get; }
    public float NumberOfEnemiesSpottedAround { get; }

    public SettlementChangedEnemiesSpotted(string settlementId, float numberOfEnemiesSpottedAround)
    {
        SettlementId = settlementId;
        NumberOfEnemiesSpottedAround = numberOfEnemiesSpottedAround;
    }
}
