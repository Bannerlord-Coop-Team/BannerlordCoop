using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateAlleyList : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        public NetworkUpdateAlleyList(string heroId, string valueId)
        {
            HeroId = heroId;
            ValueId = valueId;
        }
    }
}

