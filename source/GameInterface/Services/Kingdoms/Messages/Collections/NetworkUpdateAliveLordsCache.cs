using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateAliveLordsCache : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        public NetworkUpdateAliveLordsCache(string kingdomId, string valueId)
        {
            KingdomId = kingdomId;
            ValueId = valueId;
        }
    }
}
