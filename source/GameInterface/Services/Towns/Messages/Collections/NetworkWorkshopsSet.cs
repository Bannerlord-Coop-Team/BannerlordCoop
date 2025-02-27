using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkWorkshopsSet : ICommand
    {
        [ProtoMember(1)]
        public string TownId { get; }

        [ProtoMember(2)]
        public int Length { get; }

        public NetworkWorkshopsSet(string id, int length)
        {
            TownId = id;
            Length = length;
        }
    }
}
