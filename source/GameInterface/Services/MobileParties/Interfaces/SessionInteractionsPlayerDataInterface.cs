using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Interfaces;

public interface ISessionInteractionsPlayerDataInterface : IGameAbstraction
{
    void SetPlayerVillagersInteraction(string playerHeroId, string mobilePartyId, VillagerCampaignBehavior.PlayerInteraction interaction);
    void SetPlayerCaravanInteraction(string playerHeroId, string mobilePartyId, CaravansCampaignBehavior.PlayerInteraction interaction);
    void SetPlayerBanditsInteraction(string playerHeroId, string mobilePartyId, BanditInteractionsCampaignBehavior.PlayerInteraction interaction);
    void SetPlayerPatrolInteraction(string playerHeroId, string settlementId, CampaignTime interactedTime);
    void AddMetArenaMaster(string playerHeroId, string settlementId);
    void SetKnowTournaments(string playerHeroId, bool knowTournaments);
    void UpdateWarningTime(string playerHeroId, long warningTimeNumTicks);
    void AddSettlementSneakedIn(string playerHeroId, string settlementId);
    void UpdateDrinkThisDayInSettlement(string playerHeroId, string settlementId);
    void UpdateHasBoughtTunToParty(string playerHeroId, bool hasBought);
    void UpdateHasMetRandomBroker(string playerHeroId, bool hasMet);
    bool DailyTickDrinkThisDayInSettlement();
    bool WeeklyTickHasBoughtToTunToParty();
    void RemoveInteractedVillagersForAllPlayers(string mobilePartyId);
    void RemoveInteractedCaravanForAllPlayers(string mobilePartyId);
    void RemoveInteractedBanditsForAllPlayers(string mobilePartyId);
    void RemoveInteractedPatrolForAllPlayers(string settlementId);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionInteractionsPlayerDataInterface : ISessionInteractionsPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionInteractionsPlayerDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    private InteractionsPlayerData InteractionsPlayerData => coopSessionProvider.CoopSession.InteractionsPlayerData;

    public SessionInteractionsPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    private void SetPlayerInteraction(string playerHeroId, string mobilePartyId, int interactionInt, Dictionary<string, Dictionary<string, int>> interactionDictionary)
    {
        GameThread.RunSafe(() =>
        {
            if (!IsPlayerHeroIdValid(playerHeroId)) return;

            if (interactionDictionary[playerHeroId].ContainsKey(mobilePartyId))
            {
                interactionDictionary[playerHeroId][mobilePartyId] = interactionInt;
                return;
            }
            interactionDictionary[playerHeroId].Add(mobilePartyId, interactionInt);
        });
    }

    private void SetPlayerInteraction(string playerHeroId, string settlementId, CampaignTime interactionTime, Dictionary<string, Dictionary<string, long>> interactionDictionary)
    {
        GameThread.RunSafe(() =>
        {
            if (!IsPlayerHeroIdValid(playerHeroId)) return;

            long numTicks = interactionTime._numTicks;

            if (interactionDictionary[playerHeroId].ContainsKey(settlementId))
            {
                interactionDictionary[playerHeroId][settlementId] = numTicks;
                return;
            }
            interactionDictionary[playerHeroId].Add(settlementId, numTicks);
        });
    }

    private void AddToList(string playerHeroId, Dictionary<string, List<string>> listDictionary, string id)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        // Skip adding duplicate ids to list
        if (listDictionary[playerHeroId]?.Contains(id) == true) return;

        listDictionary[playerHeroId]?.Add(id);
    }

    public void SetPlayerVillagersInteraction(string playerHeroId, string mobilePartyId, VillagerCampaignBehavior.PlayerInteraction interaction)
    {
        SetPlayerInteraction(playerHeroId, mobilePartyId, (int)interaction, InteractionsPlayerData.PlayerInteractedVillagers);
    }

    public void SetPlayerCaravanInteraction(string playerHeroId, string mobilePartyId, CaravansCampaignBehavior.PlayerInteraction interaction)
    {
        SetPlayerInteraction(playerHeroId, mobilePartyId, (int)interaction, InteractionsPlayerData.PlayerInteractedCaravans);
    }

    public void SetPlayerBanditsInteraction(string playerHeroId, string mobilePartyId, BanditInteractionsCampaignBehavior.PlayerInteraction interaction)
    {
        SetPlayerInteraction(playerHeroId, mobilePartyId, (int)interaction, InteractionsPlayerData.PlayerInteractedBandits);
    }

    public void SetPlayerPatrolInteraction(string playerHeroId, string settlementId, CampaignTime interactionTime)
    {
        SetPlayerInteraction(playerHeroId, settlementId, interactionTime, InteractionsPlayerData.PlayerInteractedPatrols);
    }

    public void AddMetArenaMaster(string playerHeroId, string settlementId)
    {
        AddToList(playerHeroId, InteractionsPlayerData.PlayerMetArenaMasters, settlementId);
    }

    public void SetKnowTournaments(string playerHeroId, bool knowTournaments)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        InteractionsPlayerData.PlayerKnowTournaments[playerHeroId] = knowTournaments;
    }

    public void UpdateWarningTime(string playerHeroId, long warningTimeNumTicks)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        InteractionsPlayerData.PlayerWarningTime[playerHeroId] = warningTimeNumTicks;
    }

    public void AddSettlementSneakedIn(string playerHeroId, string settlementId)
    {
        AddToList(playerHeroId, InteractionsPlayerData.PlayerAlreadySneakedSettlements, settlementId);
    }

    public void UpdateDrinkThisDayInSettlement(string playerHeroId, string settlementId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[playerHeroId] = settlementId;
    }

    public void UpdateHasBoughtTunToParty(string playerHeroId, bool hasBought)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        InteractionsPlayerData.PlayerHasBoughtTunToParty[playerHeroId] = hasBought;
    }

    public void UpdateHasMetRandomBroker(string playerHeroId, bool hasMet)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        InteractionsPlayerData.PlayerHasMetRansomBroker[playerHeroId] = hasMet;
    }

    public bool DailyTickDrinkThisDayInSettlement()
    {
        bool hasUpdated = false;
        foreach (var playerData in InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement)
        {
            if (playerData.Value != null)
            {
                InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[playerData.Key] = null;
                hasUpdated = true;
            }
        }
        return hasUpdated;
    }

    public bool WeeklyTickHasBoughtToTunToParty()
    {
        bool hasUpdated = false;
        foreach (var playerData in InteractionsPlayerData.PlayerHasBoughtTunToParty)
        {
            if (playerData.Value == true)
            {
                InteractionsPlayerData.PlayerHasBoughtTunToParty[playerData.Key] = false;
                hasUpdated = true;
            }
        }
        return hasUpdated;
    }

    private void RemoveInteractedPartyForAllPlayers(string mobilePartyId, Dictionary<string, Dictionary<string, int>> interactionDictionary)
    {
        foreach (var player in playerManager.Players)
        {
            string playerHeroId = player.HeroId;
            if (!interactionDictionary.ContainsKey(playerHeroId)) continue;

            if (interactionDictionary[playerHeroId].ContainsKey(mobilePartyId))
            {
                interactionDictionary[playerHeroId].Remove(mobilePartyId);
            }
        }
    }

    private void RemoveInteractedPartyForAllPlayers(string mobilePartyId, Dictionary<string, Dictionary<string, long>> interactionDictionary)
    {
        foreach (var player in playerManager.Players)
        {
            string playerHeroId = player.HeroId;
            if (!interactionDictionary.ContainsKey(playerHeroId)) continue;

            if (interactionDictionary[playerHeroId].ContainsKey(mobilePartyId))
            {
                interactionDictionary[playerHeroId].Remove(mobilePartyId);
            }
        }
    }

    public void RemoveInteractedVillagersForAllPlayers(string mobilePartyId)
    {
        RemoveInteractedPartyForAllPlayers(mobilePartyId, InteractionsPlayerData.PlayerInteractedVillagers);
    }

    public void RemoveInteractedCaravanForAllPlayers(string mobilePartyId)
    {
        RemoveInteractedPartyForAllPlayers(mobilePartyId, InteractionsPlayerData.PlayerInteractedCaravans);
    }

    public void RemoveInteractedBanditsForAllPlayers(string mobilePartyId)
    {
        RemoveInteractedPartyForAllPlayers(mobilePartyId, InteractionsPlayerData.PlayerInteractedBandits);
    }

    public void RemoveInteractedPatrolForAllPlayers(string settlementId)
    {
        RemoveInteractedPartyForAllPlayers(settlementId, InteractionsPlayerData.PlayerInteractedPatrols);
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (InteractionsPlayerData == null)
        {
            Logger.Error("InteractionsPlayerData was null");
            return;
        }

        if (!InteractionsPlayerData.PlayerInteractedVillagers.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerInteractedVillagers[playerHeroId] = new Dictionary<string, int>();
        }
        if (!InteractionsPlayerData.PlayerInteractedCaravans.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerInteractedCaravans[playerHeroId] = new Dictionary<string, int>();
        }
        if (!InteractionsPlayerData.PlayerInteractedBandits.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerInteractedBandits[playerHeroId] = new Dictionary<string, int>();
        }
        if (!InteractionsPlayerData.PlayerInteractedPatrols.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerInteractedPatrols[playerHeroId] = new Dictionary<string, long>();
        }
        if (!InteractionsPlayerData.PlayerMetArenaMasters.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerMetArenaMasters[playerHeroId] = new List<string>();
        }
        if (!InteractionsPlayerData.PlayerKnowTournaments.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerKnowTournaments[playerHeroId] = false;
        }
        if (!InteractionsPlayerData.PlayerWarningTime.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerWarningTime[playerHeroId] = 0L;
        }
        if (!InteractionsPlayerData.PlayerAlreadySneakedSettlements.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerAlreadySneakedSettlements[playerHeroId] = new List<string>();
        }
        if (!InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[playerHeroId] = null;
        }
        if (!InteractionsPlayerData.PlayerHasBoughtTunToParty.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerHasBoughtTunToParty[playerHeroId] = false;
        }
        if (!InteractionsPlayerData.PlayerHasMetRansomBroker.ContainsKey(playerHeroId))
        {
            InteractionsPlayerData.PlayerHasMetRansomBroker[playerHeroId] = false;
        }
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging(playerHeroId, out Hero _);
    }
}
