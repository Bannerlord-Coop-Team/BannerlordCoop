using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class RemoveDecision: ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public KingdomDecisionData Data { get; }

        public RemoveDecision(string kingdomId, KingdomDecisionData data)
        {
            KingdomId = kingdomId;
            Data = data;
        }
    }
}
