using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BasicCharacterObjects.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateBasicCharacter : ICommand
    {
        [ProtoMember(1)]
        public string BasicCharacterId { get; }

        public NetworkCreateBasicCharacter(string basicCharacterId)
        {
            BasicCharacterId = basicCharacterId;
        }
    }
}
