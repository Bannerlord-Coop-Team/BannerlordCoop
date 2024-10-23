using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Towns.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateTown : ICommand
    {
        [ProtoMember(1)]
        public string TownId { get; set; }

        public NetworkCreateTown(string townId)
        {
            TownId = townId;
        }
    }
}
