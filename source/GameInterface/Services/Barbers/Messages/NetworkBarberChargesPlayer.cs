using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Barbers.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBarberChargesPlayer : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkBarberChargesPlayer(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}