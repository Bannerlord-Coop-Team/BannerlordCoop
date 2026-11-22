using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateHasMetHermit : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly bool HasMetHermit;

    public NetworkUpdateHasMetHermit(string mainHeroId, bool hasMetHermit)
    {
        MainHeroId = mainHeroId;
        HasMetHermit = hasMetHermit;
    }
}
