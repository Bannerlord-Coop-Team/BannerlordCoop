using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateVillagesCache : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        public NetworkUpdateVillagesCache(string kingdomId, string valueId)
        {
            KingdomId = kingdomId;
            ValueId = valueId;
        }
    }
}
