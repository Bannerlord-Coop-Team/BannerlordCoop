using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Caravans;

/// <summary>
/// Some data structures in CaravansCampaignBehavior are player specific and have to be managed separately
/// _prohibitedKingdomsForPlayerCaravans is unique for each player, need unique list per player
/// _interactedCaravans tracks last interaction a player had with a caravan, needs to be unique per player
/// _tradeRumorTakenCaravans keeps track of which caravans a trade rumor has been taken from by a player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class CaravansPlayerData
{
    // Dictionary<PlayerHeroId, List<KingdomId>>
    [ProtoMember(1)]
    public Dictionary<string, List<string>> PlayerProhibitedKingdomsForPlayerCaravans { get; }

    // Dictionary<PlayerHeroId, Dictionary<CaravanMobilePartyId, CaravansCampaignBehavior.PlayerInteraction>
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, int>> PlayerInteractedCaravans { get; }

    // Dictionary<PlayerHeroId, Dictionary<CaravanMobilePartyId, CampaignTime._numTicks>
    [ProtoMember(3)]
    public Dictionary<string, Dictionary<string, long>> PlayerTradeRumorTakenCaravans { get; }

    public CaravansPlayerData(
        Dictionary<string, List<string>> playerProhibitedKingdomsForPlayerCaravans,
        Dictionary<string, Dictionary<string, int>> playerInteractedCaravans,
        Dictionary<string, Dictionary<string, long>> playerTradeRumorTakenCaravans)
    {
        PlayerProhibitedKingdomsForPlayerCaravans = playerProhibitedKingdomsForPlayerCaravans;
        PlayerInteractedCaravans = playerInteractedCaravans;
        PlayerTradeRumorTakenCaravans = playerTradeRumorTakenCaravans;
    }
}
