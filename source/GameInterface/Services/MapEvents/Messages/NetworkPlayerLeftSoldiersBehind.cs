using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerLeftSoldiersBehind : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    public NetworkPlayerLeftSoldiersBehind(string mainHeroId, string mainPartyId)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
    }
}
