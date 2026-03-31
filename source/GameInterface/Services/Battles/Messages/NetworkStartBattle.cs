using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Battles.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkStartBattle : ICommand
    {
        [ProtoMember(1)]
        public string AttackerId { get; }
        [ProtoMember(2)]
        public string DefenderId { get; }
        [ProtoMember(3)]
        public bool IsSettlement { get; }

        public NetworkStartBattle(string attackerId, string defenderId, bool isSettlement)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
            IsSettlement = isSettlement;
        }
    }
}