using GameInterface.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Interfaces;

public interface INavalPatrolPartiesCampaignBehaviorInterface : IGameAbstraction
{
    void SetSettlementState(Settlement settlement, bool isQueued, CampaignTime queueTime, MobileParty patrolParty);
}

public class NavalPatrolPartiesCampaignBehaviorInterface : INavalPatrolPartiesCampaignBehaviorInterface
{
    public void SetSettlementState(Settlement settlement, bool isQueued, CampaignTime queueTime, MobileParty patrolParty)
    {
        var behavior = Campaign.Current?.GetCampaignBehavior<NavalPatrolPartiesCampaignBehavior>();
        if (behavior == null) return;

        if (isQueued)
        {
            behavior._partyGenerationQueue[settlement] = queueTime;
        }
        else
        {
            behavior._partyGenerationQueue.Remove(settlement);
        }

        if (patrolParty != null)
        {
            behavior._patrolParties[settlement] = patrolParty;
        }
        else
        {
            behavior._patrolParties.Remove(settlement);
        }
    }
}
