using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public class AddDecision: IEvent
    {
        public string KingdomId { get; }
        public KingdomDecisionData Data { get; }
        public bool IgnoreInfluenceCost { get; }

        public AddDecision(string kingdomId, KingdomDecisionData data, bool ignoreInfluenceCost)
        {
            KingdomId = kingdomId;
            Data = data;
            IgnoreInfluenceCost = ignoreInfluenceCost;
        }
    }
}
