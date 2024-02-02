using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class RemoveDecisionRequest: ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public KingdomDecisionData Data { get; }

        public RemoveDecisionRequest(string kingdomId, KingdomDecisionData data)
        {
            KingdomId = kingdomId;
            Data = data;
        }
    }
}
