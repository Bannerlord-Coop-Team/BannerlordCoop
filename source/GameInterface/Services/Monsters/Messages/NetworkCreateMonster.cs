using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Monsters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateMonster : ICommand
    {
        [ProtoMember(1)]
        public string MonsterId { get; set; }

        public NetworkCreateMonster(string monsterId)
        {
            MonsterId = monsterId;
        }
    }
}
