using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkRemoveWorkshopList : ICommand
    {
        [ProtoMember(1)]
        public uint HeroId { get; }

        [ProtoMember(2)]
        public uint ValueId { get; }

        public NetworkRemoveWorkshopList(uint heroId, uint valueId)
        {
            HeroId = heroId;
            ValueId = valueId;
        }
    }
}

