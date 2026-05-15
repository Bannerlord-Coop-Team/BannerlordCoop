using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Battles.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartPlayerBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerPartyId;

    public NetworkStartPlayerBattle(string playerPartyId)
    {
        PlayerPartyId = playerPartyId;
    }
}