using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Has Game Interface change the SEttlementWallSectionHitPointsRatioList value.
/// </summary>
public record SettlementWallHitPointsRatioChanged : IEvent
{
    public string SettlementId { get; }
    public int index { get; }
    public float hitPointsRatio { get; }

    public SettlementWallHitPointsRatioChanged(string settlementId, int index, float hitPointsRatio)
    {
        SettlementId = settlementId;
        this.index = index;
        this.hitPointsRatio = hitPointsRatio;
    }
}
