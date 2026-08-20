using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record KingdomDecisionRoundStatusChanged : IEvent
    {
        public KingdomDecisionRoundStatusData Status { get; }

        public KingdomDecisionRoundStatusChanged(KingdomDecisionRoundStatusData status)
        {
            Status = status;
        }
    }
}
