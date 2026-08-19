using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkKingdomDecisionRoundStatus : ICommand
    {
        [ProtoMember(1)]
        public KingdomDecisionRoundStatusData Status { get; }

        public NetworkKingdomDecisionRoundStatus(KingdomDecisionRoundStatusData status)
        {
            Status = status;
        }
    }
}
