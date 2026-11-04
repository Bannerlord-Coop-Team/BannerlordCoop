using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Kingdoms.Data;

/// <summary>
/// Used in CoopSession.InteractionsPlayerData to save VassalAndMercenaryOfferCampaignBehavior data per client
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class VassalAndMercenaryOfferData
{
    [ProtoMember(1)]
    public string CurrentMercenaryOfferKingdomId;

    [ProtoMember(2)]
    public long CurrentMercenaryOfferDateNumTicks;

    // Dictionary<KingdomId, CampaignTimeNumTicks>
    [ProtoMember(3)]
    public Dictionary<string, long> VassalOffers;

    [ProtoMember(4)]
    public bool StopOffers;

    public VassalAndMercenaryOfferData(
        string currentMercenaryOfferKingdomId,
        long currentMercenaryOfferDateNumTicks,
        Dictionary<string, long> vassalOffers,
        bool stopOffers)
    {
        CurrentMercenaryOfferKingdomId = currentMercenaryOfferKingdomId;
        CurrentMercenaryOfferDateNumTicks = currentMercenaryOfferDateNumTicks;
        VassalOffers = vassalOffers ?? new();
        StopOffers = stopOffers;
    }
}
