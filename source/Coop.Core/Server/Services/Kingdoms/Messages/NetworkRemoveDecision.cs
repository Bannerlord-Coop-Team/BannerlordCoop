using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkRemoveDecision : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int Index { get; }

        public NetworkRemoveDecision(string kingdomId, int index)
        {
            KingdomId = kingdomId;
            Index = index;
        }
    }
}
