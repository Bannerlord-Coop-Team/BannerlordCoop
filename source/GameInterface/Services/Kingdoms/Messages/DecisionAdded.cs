using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Kingdoms.Messages
{
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
