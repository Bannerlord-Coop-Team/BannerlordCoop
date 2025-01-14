using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateArray : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }

        [ProtoMember(2)]
        public string ValueId { get; }

        [ProtoMember(3)]
        public int Index { get; }

        public NetworkUpdateArray(string heroId, string valueId, int index)
        {
            HeroId = heroId;
            ValueId = valueId;
            Index = index;
        }
    }
}

