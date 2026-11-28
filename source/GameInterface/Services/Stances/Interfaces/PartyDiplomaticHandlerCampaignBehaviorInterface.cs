using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Stances.Interfaces;

/// <summary>
/// Runs vanilla's diplomatic settlement check outside a stance change.
/// </summary>
public interface IPartyDiplomaticHandlerCampaignBehaviorInterface : IGameAbstraction
{
    /// <summary>
    /// Server side: ejects the party from a settlement at war with it, the same way a stance change does. A player's party is
    /// only ejected once its owner is offline, because an online player's party is left to its own client.
    /// </summary>
    void CheckSettlementSuitability(MobileParty party);
}

/// <inheritdoc cref="IPartyDiplomaticHandlerCampaignBehaviorInterface"/>
public class PartyDiplomaticHandlerCampaignBehaviorInterface : IPartyDiplomaticHandlerCampaignBehaviorInterface
{
    public void CheckSettlementSuitability(MobileParty party)
    {
        Campaign.Current.GetCampaignBehavior<PartyDiplomaticHandlerCampaignBehavior>()?
            .CheckSettlementSuitabilityForParties(new[] { party });
    }
}
