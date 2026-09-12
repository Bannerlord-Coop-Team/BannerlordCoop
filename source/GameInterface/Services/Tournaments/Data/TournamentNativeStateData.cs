using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Tournaments.Data;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentNativeGameData
{
    [ProtoMember(1)]
    public readonly string TownId;
    [ProtoMember(2)]
    public readonly string PrizeItemId;
    [ProtoMember(3)]
    public readonly CampaignTime CreationTime;
    [ProtoMember(4)]
    public readonly int QualificationMode;
    [ProtoMember(5)]
    public readonly bool IsSupported;

    public TournamentNativeGameData(
        string townId,
        string prizeItemId,
        CampaignTime creationTime,
        int qualificationMode,
        bool isSupported = true)
    {
        TownId = townId;
        PrizeItemId = prizeItemId;
        CreationTime = creationTime;
        QualificationMode = qualificationMode;
        IsSupported = isSupported;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentLeaderboardEntryData
{
    [ProtoMember(1)]
    public readonly string HeroId;
    [ProtoMember(2)]
    public readonly int Wins;

    public TournamentLeaderboardEntryData(string heroId, int wins)
    {
        HeroId = heroId;
        Wins = wins;
    }
}
