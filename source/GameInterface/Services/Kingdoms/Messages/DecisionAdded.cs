using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Event that is handled on server side, when Kingdom.AddDecision method is called.
    /// </summary>
    public record DecisionAdded: IEvent
    {
        public string KingdomId { get; }

        public KingdomDecisionData Data { get; }

        public bool IgnoreInfluenceCost { get; }

        public DecisionAdded(string kingdomId, KingdomDecisionData data, bool ignoreInfluenceCost)
        {
            KingdomId = kingdomId;
            Data = data;
            IgnoreInfluenceCost = ignoreInfluenceCost;
        }
    }
}
