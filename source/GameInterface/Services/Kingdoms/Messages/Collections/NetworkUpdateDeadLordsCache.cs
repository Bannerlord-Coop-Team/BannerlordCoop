using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateDeadLordsCache : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        public NetworkUpdateDeadLordsCache(string kingdomId, string valueId)
        {
            KingdomId = kingdomId;
            ValueId = valueId;
        }
    }
}
