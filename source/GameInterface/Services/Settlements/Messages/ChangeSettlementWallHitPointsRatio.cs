using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify Server to send client hit points ratio change
/// </summary>
public record ChangeSettlementWallHitPointsRatio : ICommand
{
    public string SettlementId { get; }
    public int index { get; }
    public float hitPointsRatio { get; }

    public ChangeSettlementWallHitPointsRatio(string settlementId, int index, float hitPointsRatio)
    {
        SettlementId = settlementId;
        this.index = index;
        this.hitPointsRatio = hitPointsRatio;
    }
}
