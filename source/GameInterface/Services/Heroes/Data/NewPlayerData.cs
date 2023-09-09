namespace GameInterface.Services.Heroes.Data;

public record NewPlayerData
{
    public byte[] HeroData { get; internal set; }
    public string HeroStringId { get; internal set; }
    public string PartyStringId { get; internal set; }
    public string CharacterObjectStringId { get; internal set; }
    public string ClanStringId { get; internal set; }
}
