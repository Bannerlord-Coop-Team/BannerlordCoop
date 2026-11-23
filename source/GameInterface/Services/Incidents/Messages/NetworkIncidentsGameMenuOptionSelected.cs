using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkIncidentsGameMenuOptionSelected : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string GameMenuId;

    [ProtoMember(4)]
    public readonly string OptionId;

    public NetworkIncidentsGameMenuOptionSelected(
        string mainHeroId,
        string mainPartyId,
        string gameMenuId,
        string optionId)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        GameMenuId = gameMenuId;
        OptionId = optionId;
    }
}
