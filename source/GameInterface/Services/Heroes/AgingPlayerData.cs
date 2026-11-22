using GameInterface.Services.Heroes.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes;

/// <summary>
/// The following assume only one player
/// Hero.MainHeroIllDays
/// Player succession information also has to be separately tracked
/// MarriageOfferCampaignBehavior current marriage offer and offer cooldown
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class AgingPlayerData
{
    // Dictionary<PlayerHeroId, IsIllDays>
    [ProtoMember(1)]
    public Dictionary<string, int> PlayerIsIllDays { get; }

    [ProtoMember(2)]
    public Dictionary<string, PlayerSuccessionData> PlayerSuccessions { get; }

    // Dictionary<PlayerHeroId, MarriageOfferData>
    [ProtoMember(3)]
    public Dictionary<string, MarriageOfferData> PlayerMarriageOffers { get; }

    public AgingPlayerData(
        Dictionary<string, int> playerIsIllDays,
        Dictionary<string, PlayerSuccessionData> playerSuccessions = null,
        Dictionary<string, MarriageOfferData> playerMarriageOffers = null)
    {
        PlayerIsIllDays = playerIsIllDays ?? new();
        PlayerSuccessions = playerSuccessions ?? new();
        PlayerMarriageOffers = playerMarriageOffers ?? new();
    }
}

[ProtoContract(SkipConstructor = true)]
public class PlayerSuccessionData
{
    [ProtoMember(1)]
    public string SpouseId { get; set; }
    [ProtoMember(2)]
    public bool DeathRecorded { get; set; }
    [ProtoMember(3)]
    public bool WasClanLeader { get; set; }
    [ProtoMember(4)]
    public double? AppointmentDeadlineDays { get; set; }
    [ProtoMember(5)]
    public bool GameOver { get; set; }
    [ProtoMember(6)]
    public string AppointedLeaderId { get; set; }
}
