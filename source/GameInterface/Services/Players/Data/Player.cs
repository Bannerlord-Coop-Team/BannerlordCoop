using ProtoBuf;

namespace GameInterface.Services.Players.Data;

[ProtoContract]
public class Player
{
    [ProtoMember(1)]
    public readonly string ControllerId;
    [ProtoMember(2)]
    public readonly string HeroId;
    [ProtoMember(3)]
    public readonly string MobilePartyId;
    [ProtoMember(4)]
    public readonly string ClanId;
    [ProtoMember(5)]
    public readonly string CharacterObjectId;

    public Player(string controllerId, string heroId, string mobilePartyId, string clanId, string characterObjectId)
    {
        ControllerId = controllerId;
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        ClanId = clanId;
        CharacterObjectId = characterObjectId;
    }
}
