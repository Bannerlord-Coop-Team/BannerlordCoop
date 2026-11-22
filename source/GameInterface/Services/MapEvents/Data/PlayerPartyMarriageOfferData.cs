using ProtoBuf;

namespace GameInterface.Services.MapEvents.Data;

[ProtoContract(SkipConstructor = true)]
public struct PlayerPartyMarriageOfferData
{
    [ProtoMember(1)]
    public string HeroId { get; set; }
    [ProtoMember(2)]
    public string SpouseId { get; set; }

    public PlayerPartyMarriageOfferData(string heroId, string spouseId)
    {
        HeroId = heroId;
        SpouseId = spouseId;
    }
}
