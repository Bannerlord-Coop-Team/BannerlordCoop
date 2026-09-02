using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes;

/// <summary>
/// The following assume only one player
/// Hero.MainHeroIllDays
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class AgingPlayerData
{
    // Dictionary<PlayerHeroId, IsIllDays>
    [ProtoMember(1)]
    public Dictionary<string, int> PlayerIsIllDays { get; }

    public AgingPlayerData(
        Dictionary<string, int> playerIsIllDays)
    {
        PlayerIsIllDays = playerIsIllDays ?? new();
    }
}
