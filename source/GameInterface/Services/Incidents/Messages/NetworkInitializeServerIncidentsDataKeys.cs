using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Incidents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkInitializeServerIncidentsDataKeys : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    public NetworkInitializeServerIncidentsDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}
