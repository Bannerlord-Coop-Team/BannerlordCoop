using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkStartBattle : ICommand
    {
        [ProtoMember(1)]
        public string AttackerId { get; }
        [ProtoMember(2)]
        public string DefenderId { get; }

        public NetworkStartBattle(string attackerId, string defenderId)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
        }
    }
}