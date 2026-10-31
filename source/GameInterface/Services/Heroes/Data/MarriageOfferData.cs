using ProtoBuf;

namespace GameInterface.Services.Heroes.Data;

/// <summary>
/// Used in CoopSession.AgingPlayerData to save MarriageOfferCampaignBehavior offer data per client
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class MarriageOfferData
{
    [ProtoMember(1)]
    public string CurrentOfferedPlayerClanHeroId;

    [ProtoMember(2)]
    public string CurrentOfferedOtherClanHeroId;

    [ProtoMember(3)]
    public long LastMarriageOfferTimeNumTicks;

    public MarriageOfferData(
        string currentOfferedPlayerClanHeroId,
        string currentOfferedOtherClanHeroId,
        long lastMarriageOfferTimeNumTicks)
    {
        CurrentOfferedPlayerClanHeroId = currentOfferedPlayerClanHeroId;
        CurrentOfferedOtherClanHeroId = currentOfferedOtherClanHeroId;
        LastMarriageOfferTimeNumTicks = lastMarriageOfferTimeNumTicks;
    }
}
