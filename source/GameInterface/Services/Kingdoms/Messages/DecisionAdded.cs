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
    /// <summary>
    /// True when the decision was queued in <c>Kingdom._unresolvedDecisions</c>, false when it was resolved in place.
    /// Null when the publisher only proposed the decision and is waiting for the server to answer.
    /// </summary>
    public readonly bool? WasQueued;
    public DecisionAdded(Kingdom kingdom, KingdomDecision decision, bool ignoreInfluenceCost, float randomNumber, bool? wasQueued)
    {
        Kingdom = kingdom;
        Decision = decision;
        IgnoreInfluenceCost = ignoreInfluenceCost;
        RandomNumber = randomNumber;
        WasQueued = wasQueued;
    }
}
