using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Changes Clients Settlement.CanBeClaimed Value from SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged();
/// </summary>
public record ChangeSettlementClaimantCanBeClaimed : IEvent
{
    public string SettlementId { get; }
    public int CanBeClaimed { get; }

    public ChangeSettlementClaimantCanBeClaimed(string settlementId, int canBeClaimed)
    {
        SettlementId = settlementId;
        CanBeClaimed = canBeClaimed;
    }
}
