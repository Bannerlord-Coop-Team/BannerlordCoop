using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdatePlayerIllDays : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly int NewIllDays;

    public NetworkUpdatePlayerIllDays(
        string playerHeroId,
        int newIllDays)
    {
        PlayerHeroId = playerHeroId;
        NewIllDays = newIllDays;
    }
}
