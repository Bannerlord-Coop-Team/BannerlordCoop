using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Stores the last time each player met each hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class HeroMeetingData
{
    // Dictionary<PlayerHeroId, Dictionary<MetHeroId, CampaignTime._numTicks>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, long>> PlayerLastMeetingTimes { get; }

    public HeroMeetingData(Dictionary<string, Dictionary<string, long>> playerLastMeetingTimes)
    {
        PlayerLastMeetingTimes = playerLastMeetingTimes;
    }
}
