using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateVillage : ICommand
    {
        [ProtoMember(1)]
        public string VillageId { get; }

        public NetworkCreateVillage(string villageId)
        {
            VillageId = villageId;
        }
    }
}
