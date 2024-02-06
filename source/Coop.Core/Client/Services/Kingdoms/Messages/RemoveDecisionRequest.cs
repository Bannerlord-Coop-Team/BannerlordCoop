using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class RemoveDecisionRequest: ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int Index { get; }

        public RemoveDecisionRequest(string kingdomId, int index)
        {
            KingdomId = kingdomId;
            Index = index;
        }
    }
}
