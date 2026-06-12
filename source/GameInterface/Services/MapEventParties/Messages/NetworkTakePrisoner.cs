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
    [ProtoMember(3)]
    public readonly string PrisonerPartyId;

    public NetworkTakePrisoner(string partyBaseId, string heroId, string prisonerPartyId)
    {
        PartyBaseId = partyBaseId;
        HeroId = heroId;
        PrisonerPartyId = prisonerPartyId;
    }
}