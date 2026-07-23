using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkRemoveCaravanList : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        public NetworkRemoveCaravanList(string heroId, string valueId)
        {
            HeroId = heroId;
            ValueId = valueId;
        }
    }
}

