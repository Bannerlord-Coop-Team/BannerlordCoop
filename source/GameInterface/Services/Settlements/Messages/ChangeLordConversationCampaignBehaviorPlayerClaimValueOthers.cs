using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Clients change Settlement.ClaimValue
/// </summary>
public record ChangeLordConversationCampaignBehaviorPlayerClaimValueOthers : ICommand
{
    public string SettlementId { get; }
    public float ClaimValue { get; }

    public ChangeLordConversationCampaignBehaviorPlayerClaimValueOthers(string settlementId, float claimValue)
    {
        SettlementId = settlementId;
        ClaimValue = claimValue;
    }
}
