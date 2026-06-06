using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

[ProtoContract]
public readonly struct NetworkTakePrisoner : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyBaseId;
    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkTakePrisoner(string partyBaseId, string heroId)
    {
        PartyBaseId = partyBaseId;
        HeroId = heroId;
    }
}