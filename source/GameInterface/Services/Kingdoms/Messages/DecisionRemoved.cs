using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record DecisionRemoved : IEvent
    {
        public string KingdomId { get; }

        public KingdomDecisionData Data { get; }

        public DecisionRemoved(string kingdomId, KingdomDecisionData data)
        {
            KingdomId = kingdomId;
            Data = data;
        }
    }
}
