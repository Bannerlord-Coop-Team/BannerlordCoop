using ProtoBuf;

namespace GameInterface.Services.Players.Data;

[ProtoContract]
public readonly struct Player
{
    [ProtoMember(1)]
    public readonly string HeroId;
    [ProtoMember(2)]
    public readonly string MobilePartyId;
    [ProtoMember(3)]
    public readonly string ClanId;
    [ProtoMember(4)]
    public readonly string CharacterObjectId;

    public Player(string heroId, string mobilePartyId, string clanId, string characterObjectId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        ClanId = clanId;
        CharacterObjectId = characterObjectId;
    }
}
