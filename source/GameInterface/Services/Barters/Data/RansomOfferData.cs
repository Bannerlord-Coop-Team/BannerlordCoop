using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Barters.Data;

/// <summary>
/// Used in CoopSession.TradePlayerData to save RansomOfferCampaignBehavior data per client
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class RansomOfferData
{
    [ProtoMember(1)]
    public List<string> HeroIdsWithDeclinedRansomOffers;

    [ProtoMember(2)]
    public string CurrentRansomHeroId;

    [ProtoMember(3)]
    public string CurrentRansomPayerId;

    [ProtoMember(4)]
    public long CurrentRansomOfferDateNumTicks;

    [ProtoMember(5)]
    public float CurrentRansomPrice;

    public RansomOfferData(
        List<string> heroIdsWithDeclinedRansomOffers,
        string currentRansomHeroId,
        string currentRansomPayerId,
        long currentRansomOfferDateNumTicks,
        float currentRansomPrice)
    {
        HeroIdsWithDeclinedRansomOffers = heroIdsWithDeclinedRansomOffers;
        CurrentRansomHeroId = currentRansomHeroId;
        CurrentRansomPayerId = currentRansomPayerId;
        CurrentRansomOfferDateNumTicks = currentRansomOfferDateNumTicks;
        CurrentRansomPrice = currentRansomPrice;
    }
}
