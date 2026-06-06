using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Notifies GI to Server Settlement.CanBeClaimed value SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged();
/// </summary>
public readonly struct SettlementClaimantCanBeClaimedChanged : ICommand
{
    public readonly Settlement Settlement;
    public readonly int CanBeClaimed;

    public SettlementClaimantCanBeClaimedChanged(Settlement settlement, int canBeClaimed)
    {
        Settlement = settlement;
        CanBeClaimed = canBeClaimed;
    }
}
