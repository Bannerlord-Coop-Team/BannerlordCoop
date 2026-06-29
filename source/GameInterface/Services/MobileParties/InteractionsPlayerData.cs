using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Some data structures across several campaign behaviors manage the last interactions a player had with a party
/// _interactedVillagers tracks last interaction a player had with a villager party, needs to be unique per player
/// _interactedCaravans tracks last interaction a player had with a caravan, needs to be unique per player
/// _interactedBandits tracks last interaction a player had with a bandit party, needs to be unique per player
/// _interactedPatrolParties tracks last interaction a player had with a patrol, needs to be unique per player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class InteractionsPlayerData
{
    // Dictionary<PlayerHeroId, Dictionary<VillagerMobilePartyId, VillagerCampaignBehavior.PlayerInteraction>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, int>> PlayerInteractedVillagers { get; }

    // Dictionary<PlayerHeroId, Dictionary<CaravanMobilePartyId, CaravansCampaignBehavior.PlayerInteraction>
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, int>> PlayerInteractedCaravans { get; }

    // Dictionary<PlayerHeroId, Dictionary<BanditMobilePartyId, BanditInteractionsCampaignBehavior.PlayerInteraction>
    [ProtoMember(3)]
    public Dictionary<string, Dictionary<string, int>> PlayerInteractedBandits { get; }

    // Dictionary<PlayerHeroId, Dictionary<PatrolMobilePartyId, CampaignTime>
    [ProtoMember(4)]
    public Dictionary<string, Dictionary<string, long>> PlayerInteractedPatrols { get; }

    public InteractionsPlayerData(
        Dictionary<string, Dictionary<string, int>> playerInteractedVillagers,
        Dictionary<string, Dictionary<string, int>> playerInteractedCaravans,
        Dictionary<string, Dictionary<string, int>> playerInteractedBandits,
        Dictionary<string, Dictionary<string, long>> playerInteractedPatrols)
    {
        PlayerInteractedVillagers = playerInteractedVillagers;
        PlayerInteractedCaravans = playerInteractedCaravans;
        PlayerInteractedBandits = playerInteractedBandits;
        PlayerInteractedPatrols = playerInteractedPatrols;
    }
}
