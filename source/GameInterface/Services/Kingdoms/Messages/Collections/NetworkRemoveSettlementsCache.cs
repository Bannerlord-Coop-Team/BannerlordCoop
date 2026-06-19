using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkRemoveSettlementsCache : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        public NetworkRemoveSettlementsCache(string kingdomId, string valueId)
        {
            KingdomId = kingdomId;
            ValueId = valueId;
        }
    }
}
