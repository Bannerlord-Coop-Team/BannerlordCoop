using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Server changes claim
/// </summary>
public record ChangeLordConversationCampaignBehaviorPlayerClaim : ICommand
{
    public string SettlementId { get; }
    public string HeroId { get; }

    public ChangeLordConversationCampaignBehaviorPlayerClaim(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
