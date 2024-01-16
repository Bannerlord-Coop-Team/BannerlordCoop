using ProtoBuf;

namespace GameInterface.Services.Players.Data;

[ProtoContract]
public record Player
{
    public Player(byte[] heroData, string heroStringId, string partyStringId, string characterObjectStringId, string clanStringId)
    {
        HeroData = heroData;
        HeroStringId = heroStringId;
        PartyStringId = partyStringId;
        CharacterObjectStringId = characterObjectStringId;
        ClanStringId = clanStringId;
    }

    [ProtoMember(1)]
    public byte[] HeroData { get; internal set; }
    [ProtoMember(2)]
    public string HeroStringId { get; internal set; }
    [ProtoMember(3)]
    public string PartyStringId { get; internal set; }
    [ProtoMember(4)]
    public string CharacterObjectStringId { get; internal set; }
    [ProtoMember(5)]
    public string ClanStringId { get; internal set; }
}
