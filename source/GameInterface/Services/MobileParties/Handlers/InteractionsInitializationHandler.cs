using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.CampaignBehaviors;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

internal class InteractionsInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<InteractionsInitializationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    private InteractionsPlayerData interactionsPlayerData;

    public InteractionsInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<InitializeClientInteractionsData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<NetworkInitializeServerInteractionsDataKeys>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientInteractionsData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeServerInteractionsDataKeys>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientInteractionsData> obj)
    {
        interactionsPlayerData = obj.What.InteractionsPlayerData;
    }

    // Need to load interactions data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        VillagerCampaignBehavior villagerCampaignBehavior = Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>();
        CaravansCampaignBehavior caravansCampaignBehavior = Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>();
        BanditInteractionsCampaignBehavior banditInteractionsCampaignBehavior = Campaign.Current.GetCampaignBehavior<BanditInteractionsCampaignBehavior>();
        PatrolPartiesCampaignBehavior patrolPartiesCampaignBehavior = Campaign.Current.GetCampaignBehavior<PatrolPartiesCampaignBehavior>();
        ArenaMasterCampaignBehavior arenaMasterCampaignBehavior = Campaign.Current.GetCampaignBehavior<ArenaMasterCampaignBehavior>();
        PerkResetCampaignBehavior perkResetCampaignBehavior = Campaign.Current.GetCampaignBehavior<PerkResetCampaignBehavior>();
        EncounterGameMenuBehavior encounterGameMenuBehavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();

        villagerCampaignBehavior._interactedVillagers = GetInteractedVillagers(playerHeroId);
        caravansCampaignBehavior._interactedCaravans = GetInteractedCaravans(playerHeroId);
        banditInteractionsCampaignBehavior._interactedBandits = GetInteractedBandits(playerHeroId);
        patrolPartiesCampaignBehavior._interactedPatrolParties = GetInteractedPatrols(playerHeroId);
        arenaMasterCampaignBehavior._arenaMasterHasMetInSettlements = GetMetArenaMasters(playerHeroId);
        arenaMasterCampaignBehavior._knowTournaments = GetKnowTournaments(playerHeroId);
        perkResetCampaignBehavior._warningTime = GetWarningTime(playerHeroId);
        encounterGameMenuBehavior._alreadySneakedSettlements = GetAlreadySneakedSettlements(playerHeroId);

        network.SendAll(new NetworkInitializeServerInteractionsDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerInteractionsDataKeys> obj)
    {
        sessionInteractionsPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
    }

    private Dictionary<MobileParty, VillagerCampaignBehavior.PlayerInteraction> GetInteractedVillagers(string playerHeroId)
    {
        var interactedVillagers = new Dictionary<MobileParty, VillagerCampaignBehavior.PlayerInteraction>();

        // Null and key check for players without existing interacted villagers data
        if (interactionsPlayerData?.PlayerInteractedVillagers?.ContainsKey(playerHeroId) != true) return interactedVillagers;

        foreach (KeyValuePair<string, int> villagersInteraction in interactionsPlayerData.PlayerInteractedVillagers[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(villagersInteraction.Key, out var villagers)) continue;

            interactedVillagers[villagers] = (VillagerCampaignBehavior.PlayerInteraction)villagersInteraction.Value;
        }

        return interactedVillagers;
    }

    private Dictionary<MobileParty, CaravansCampaignBehavior.PlayerInteraction> GetInteractedCaravans(string playerHeroId)
    {
        var interactedCaravans = new Dictionary<MobileParty, CaravansCampaignBehavior.PlayerInteraction>();

        // Null and key check for players without existing interacted caravans data
        if (interactionsPlayerData?.PlayerInteractedCaravans?.ContainsKey(playerHeroId) != true) return interactedCaravans;

        foreach (KeyValuePair<string, int> caravanInteraction in interactionsPlayerData.PlayerInteractedCaravans[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(caravanInteraction.Key, out var caravan)) continue;

            interactedCaravans[caravan] = (CaravansCampaignBehavior.PlayerInteraction)caravanInteraction.Value;
        }

        return interactedCaravans;
    }

    private Dictionary<MobileParty, BanditInteractionsCampaignBehavior.PlayerInteraction> GetInteractedBandits(string playerHeroId)
    {
        var interactedBandits = new Dictionary<MobileParty, BanditInteractionsCampaignBehavior.PlayerInteraction>();

        // Null and key check for players without existing interacted caravans data
        if (interactionsPlayerData?.PlayerInteractedBandits?.ContainsKey(playerHeroId) != true) return interactedBandits;

        foreach (KeyValuePair<string, int> banditsInteraction in interactionsPlayerData.PlayerInteractedBandits[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(banditsInteraction.Key, out var bandits)) continue;

            interactedBandits[bandits] = (BanditInteractionsCampaignBehavior.PlayerInteraction)banditsInteraction.Value;
        }

        return interactedBandits;
    }

    private Dictionary<Settlement, CampaignTime> GetInteractedPatrols(string playerHeroId)
    {
        var interactedPatrols = new Dictionary<Settlement, CampaignTime>();

        // Null and key check for players without existing interacted caravans data
        if (interactionsPlayerData?.PlayerInteractedPatrols?.ContainsKey(playerHeroId) != true) return interactedPatrols;

        foreach (KeyValuePair<string, long> patrolInteraction in interactionsPlayerData.PlayerInteractedPatrols[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(patrolInteraction.Key, out var settlement)) continue;

            interactedPatrols[settlement] = new CampaignTime(patrolInteraction.Value);
        }

        return interactedPatrols;
    }

    private List<Settlement> GetMetArenaMasters(string playerHeroId)
    {
        var metArenaMasters = new List<Settlement>();

        // Null and key check for players without existing interacted caravans data
        if (interactionsPlayerData?.PlayerMetArenaMasters?.ContainsKey(playerHeroId) != true) return metArenaMasters;

        foreach (var arenaMasterSettlementId in interactionsPlayerData.PlayerMetArenaMasters[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(arenaMasterSettlementId, out var settlement)) continue;

            metArenaMasters.Add(settlement);
        }

        return metArenaMasters;
    }

    private bool GetKnowTournaments(string playerHeroId)
    {
        if (interactionsPlayerData?.PlayerKnowTournaments?.ContainsKey(playerHeroId) != true) return false;

        return interactionsPlayerData.PlayerKnowTournaments[playerHeroId];
    }

    private CampaignTime GetWarningTime(string playerHeroId)
    {
        if (interactionsPlayerData?.PlayerWarningTime?.ContainsKey(playerHeroId) != true) return CampaignTime.Zero;

        return new CampaignTime(interactionsPlayerData.PlayerWarningTime[playerHeroId]);
    }

    private List<Settlement> GetAlreadySneakedSettlements(string playerHeroId)
    {
        var alreadySneakedSettlements = new List<Settlement>();

        // Null and key check for players without existing interacted caravans data
        if (interactionsPlayerData?.PlayerAlreadySneakedSettlements?.ContainsKey(playerHeroId) != true) return alreadySneakedSettlements;

        foreach (var sneakedSettlementId in interactionsPlayerData.PlayerAlreadySneakedSettlements[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(sneakedSettlementId, out var settlement)) continue;

            alreadySneakedSettlements.Add(settlement);
        }

        return alreadySneakedSettlements;
    }
}
