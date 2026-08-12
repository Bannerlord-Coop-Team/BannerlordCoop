using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Some data structures across several campaign behaviors manage the last interactions a player had with a party
/// _interactedVillagers tracks last interaction a player had with a villager party, needs to be unique per player
/// _interactedCaravans tracks last interaction a player had with a caravan, needs to be unique per player
/// _interactedBandits tracks last interaction a player had with a bandit party, needs to be unique per player
/// _interactedPatrolParties tracks last interaction a player had with a patrol, needs to be unique per player
/// _arenaMasterHasMetInSettlements tracks arena masters a player has met, needs to be unique per player
/// _knowTournaments is used to allow a player to enter practice fights after they have spoken with an arena master
/// _warningTime is used to give clients a 6 day warning before sending a message to the server to remove a companion
/// _alreadySneakedSettlements is used to save when a player establishes a contact in a settlement
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

    // Dictionary<PlayerHeroId, List<SettlementId>>
    [ProtoMember(5)]
    public Dictionary<string, List<string>> PlayerMetArenaMasters { get; }

    // Dictionary<PlayerHeroId, KnowTournaments>
    [ProtoMember(6)]
    public Dictionary<string, bool> PlayerKnowTournaments { get; }

    // Dictionary<PlayerHeroId, CampaignTimeNumTicks>
    [ProtoMember(7)]
    public Dictionary<string, long> PlayerWarningTime { get; }

    // Dictionary<PlayerHeroId, List<SettlementId>>
    [ProtoMember(8)]
    public Dictionary<string, List<string>> PlayerAlreadySneakedSettlements { get; }

    public InteractionsPlayerData(
        Dictionary<string, Dictionary<string, int>> playerInteractedVillagers,
        Dictionary<string, Dictionary<string, int>> playerInteractedCaravans,
        Dictionary<string, Dictionary<string, int>> playerInteractedBandits,
        Dictionary<string, Dictionary<string, long>> playerInteractedPatrols,
        Dictionary<string, List<string>> playerMetArenaMasters,
        Dictionary<string, bool> playerKnowTournaments,
        Dictionary<string, long> playerWarningTime,
        Dictionary<string, List<string>> playerAlreadySneakedSettlements)
    {
        PlayerInteractedVillagers = playerInteractedVillagers ?? new();
        PlayerInteractedCaravans = playerInteractedCaravans ?? new();
        PlayerInteractedBandits = playerInteractedBandits ?? new();
        PlayerInteractedPatrols = playerInteractedPatrols ?? new();
        PlayerMetArenaMasters = playerMetArenaMasters ?? new();
        PlayerKnowTournaments = playerKnowTournaments ?? new();
        PlayerWarningTime = playerWarningTime ?? new();
        PlayerAlreadySneakedSettlements = playerAlreadySneakedSettlements ?? new();
    }
}
