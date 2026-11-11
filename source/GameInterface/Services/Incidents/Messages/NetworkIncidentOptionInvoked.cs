using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkIncidentOptionInvoked : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string IncidentId;

    [ProtoMember(4)]
    public readonly string OptionTextId;

    public NetworkIncidentOptionInvoked(
        string mainHeroId,
        string mainPartyId,
        string incidentId,
        string optionTextId)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        IncidentId = incidentId;
        OptionTextId = optionTextId;
    }
}
