using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ApplyKingdomDecisionRoundStatus : ICommand
    {
        public KingdomDecisionRoundStatusData Status { get; }

        public ApplyKingdomDecisionRoundStatus(KingdomDecisionRoundStatusData status)
        {
            Status = status;
        }
    }
}
