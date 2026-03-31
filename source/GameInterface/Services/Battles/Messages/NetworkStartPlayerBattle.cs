using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Battles.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkStartPlayerBattle : ICommand
    {
        [ProtoMember(1)]
        public string PlayerPartyId { get; }

        public NetworkStartPlayerBattle(string playerPartyId)
        {
            PlayerPartyId = playerPartyId;
        }
    }
}