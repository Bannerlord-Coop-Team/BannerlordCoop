using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.VillageTypes.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateVillageType : ICommand
    {
        [ProtoMember(1)]
        public string VillageTypeId { get; }

        public NetworkCreateVillageType(string villageTypeId)
        {
            VillageTypeId = villageTypeId;
        }
    }
}
