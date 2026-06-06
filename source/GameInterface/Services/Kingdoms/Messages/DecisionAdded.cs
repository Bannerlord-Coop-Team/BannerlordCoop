using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Event that is handled on server side when Kingdom.AddDecision method is called.
/// </summary>
public readonly struct DecisionAdded : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly KingdomDecisionData Data;
    public readonly bool IgnoreInfluenceCost;
    public readonly float RandomNumber;

    public DecisionAdded(Kingdom kingdom, KingdomDecisionData data, bool ignoreInfluenceCost, float randomNumber)
    {
        Kingdom = kingdom;
        Data = data;
        IgnoreInfluenceCost = ignoreInfluenceCost;
        RandomNumber = randomNumber;
    }
}