using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

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