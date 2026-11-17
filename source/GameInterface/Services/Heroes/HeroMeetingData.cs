using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Stores the last time each player met each hero.
/// Also tracks scattered companions for each player.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class HeroMeetingData
{
    // Dictionary<PlayerHeroId, Dictionary<MetHeroId, CampaignTime._numTicks>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, long>> PlayerLastMeetingTimes { get; }

    // Dictionary<PlayerHeroId, Dictionary<CompanionHeroId, CampaignTime._numTicks>>
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, long>> PlayerScatteredCompanions { get; }

    public HeroMeetingData(
        Dictionary<string, Dictionary<string, long>> playerLastMeetingTimes,
        Dictionary<string, Dictionary<string, long>> playerScatteredCompanions = null)
    {
        PlayerLastMeetingTimes = playerLastMeetingTimes ?? new();
        PlayerScatteredCompanions = playerScatteredCompanions ?? new();
    }
}
