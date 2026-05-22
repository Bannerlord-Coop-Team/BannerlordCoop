using ProtoBuf;

namespace GameInterface.Services.Players.Data;

/// <summary>
/// Holds information about the players.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPlayerData
{
    [ProtoMember(1)]
    public byte[] HeroData { get; set; }
    [ProtoMember(2)]
    public string HeroStringId { get; set; }
    [ProtoMember(3)]
    public string PartyStringId { get; set; }
    [ProtoMember(4)]
    public string CharacterObjectStringId { get; set; }
    [ProtoMember(5)]
    public string ClanStringId { get; set; }
}
