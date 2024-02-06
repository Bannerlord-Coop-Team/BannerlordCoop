using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class RemoveDecision: ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int Index { get; }

        public RemoveDecision(string kingdomId, int index)
        {
            KingdomId = kingdomId;
            Index = index;
        }
    }
}
