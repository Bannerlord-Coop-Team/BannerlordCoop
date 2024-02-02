using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class RemoveDecisionApproved: ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public KingdomDecisionData Data { get; }

        public RemoveDecisionApproved(string kingdomId, KingdomDecisionData data)
        {
            KingdomId = kingdomId;
            Data = data;
        }
    }
}
