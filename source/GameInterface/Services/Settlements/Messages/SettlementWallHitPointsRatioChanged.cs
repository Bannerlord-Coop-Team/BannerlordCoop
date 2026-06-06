using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Has Game Interface change the settlement wall section hit points ratio list value.
/// </summary>
public readonly struct SettlementWallHitPointsRatioChanged : IEvent
{
    public readonly Settlement Settlement;
    public readonly int Index;
    public readonly float HitPointsRatio;

    public SettlementWallHitPointsRatioChanged(Settlement settlement, int index, float hitPointsRatio)
    {
        Settlement = settlement;
        Index = index;
        HitPointsRatio = hitPointsRatio;
    }
}