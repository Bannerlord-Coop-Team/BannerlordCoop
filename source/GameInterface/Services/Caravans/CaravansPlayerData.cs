using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Caravans;

/// <summary>
/// Some data structures in CaravansCampaignBehavior are player specific and have to be managed separately
/// _prohibitedKingdomsForPlayerCaravans is unique for each player, need unique list per player
/// _interactedCaravans tracks last interaction a player had with a caravan, needs to be unique per player
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

    public CaravansPlayerData(
        Dictionary<string, List<string>> playerProhibitedKingdomsForPlayerCaravans,
        Dictionary<string, Dictionary<string, int>> playerInteractedCaravans)
    {
        PlayerProhibitedKingdomsForPlayerCaravans = playerProhibitedKingdomsForPlayerCaravans;
        PlayerInteractedCaravans = playerInteractedCaravans;
    }
}
