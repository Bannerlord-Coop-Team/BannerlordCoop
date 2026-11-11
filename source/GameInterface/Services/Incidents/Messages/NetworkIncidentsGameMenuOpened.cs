using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkIncidentsGameMenuOpened : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string MenuContextId;

    public NetworkIncidentsGameMenuOpened(
        string mainHeroId,
        string mainPartyId,
        string menuContextId)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        MenuContextId = menuContextId;
    }
}
