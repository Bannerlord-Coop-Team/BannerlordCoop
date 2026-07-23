using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
namespace GameInterface.Services.Kingdoms.Messages;
/// <summary>
/// Event that is handled on server side when Kingdom.AddDecision method is called.
/// </summary>
public readonly struct DecisionAdded : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly KingdomDecision Decision;
    public readonly bool IgnoreInfluenceCost;
    public readonly float RandomNumber;
    public DecisionAdded(Kingdom kingdom, KingdomDecision decision, bool ignoreInfluenceCost, float randomNumber)
    {
        Kingdom = kingdom;
        Decision = decision;
        IgnoreInfluenceCost = ignoreInfluenceCost;
        RandomNumber = randomNumber;
    }
}
