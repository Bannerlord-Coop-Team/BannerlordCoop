using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Notifies GI to Server Settlement.CanBeClaimed value SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged();
/// </summary>
public record SettlementClaimantCanBeClaimedChanged : ICommand
{
    public string SettlementId { get; }
    public int CanBeClaimed { get; }

    public SettlementClaimantCanBeClaimedChanged(string settlementId, int canBeClaimed)
    {
        SettlementId = settlementId;
        CanBeClaimed = canBeClaimed;
    }
}
