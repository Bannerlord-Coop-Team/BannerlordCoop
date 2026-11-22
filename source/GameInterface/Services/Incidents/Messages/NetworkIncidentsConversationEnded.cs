using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkIncidentsConversationEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    public NetworkIncidentsConversationEnded(string mainHeroId, string mainPartyId)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
    }
}
