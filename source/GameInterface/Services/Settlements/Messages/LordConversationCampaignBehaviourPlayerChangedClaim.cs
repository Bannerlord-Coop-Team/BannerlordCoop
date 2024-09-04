using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// When client requests to change claim
/// </summary>
[BatchLogMessage]
public record LordConversationCampaignBehaviourPlayerChangedClaim : ICommand
{
    public string SettlementId { get; }
    public string HeroId { get; }

    public LordConversationCampaignBehaviourPlayerChangedClaim(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
