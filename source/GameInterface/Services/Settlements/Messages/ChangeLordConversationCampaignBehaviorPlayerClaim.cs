using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
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
