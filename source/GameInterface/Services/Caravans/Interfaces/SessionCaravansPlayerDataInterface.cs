using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Collections;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Caravans.Interfaces;

public interface ISessionCaravansPlayerDataInterface : IGameAbstraction
{
    void AddProhibitedKingdom(string playerHeroId, string kingdomId);
    void RemoveProhibitedKingdom(string playerHeroId, string kingdomId);
    void RemoveProhibitedKingdomForAllPlayers(string kingdomId);
    void SetPlayerInteraction(string playerHeroId, string mobilePartyId, CaravansCampaignBehavior.PlayerInteraction interaction);
    void RemoveInteractedCaravanForAllPlayers(string mobilePartyId);
    void UpdateTradeRumorTakenCaravansForPlayer(string playerHeroId, Dictionary<string, CampaignTime> tradeRumorTakenCaravansIds);
    void DeleteExpiredTradeRumorTakenCaravans(out Dictionary<string, List<string>> playerExpiredCaravansRemovalLists);
    bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction, MobileParty mobileParty);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionCaravansPlayerDataInterface : ISessionCaravansPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionCaravansPlayerDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    private CaravansPlayerData CaravansPlayerData => coopSessionProvider.CoopSession.CaravansPlayerData;

    public SessionCaravansPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public void AddProhibitedKingdom(string playerHeroId, string kingdomId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId].Add(kingdomId);
    }

    public void RemoveProhibitedKingdom(string playerHeroId, string kingdomId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId].Remove(kingdomId);
    }

    // Called on a CaravansCampaignBehavior.OnKingdomDestroyed patch
    public void RemoveProhibitedKingdomForAllPlayers(string kingdomId)
    {
        foreach (var player in playerManager.Players)
        {
            string playerHeroId = player.HeroId;
            if (CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId].Contains(kingdomId))
            {
                CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId].Remove(kingdomId);
            }
        }
    }

    public void SetPlayerInteraction(string playerHeroId, string mobilePartyId, CaravansCampaignBehavior.PlayerInteraction interaction)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        // Cast to save, can't use enumerable directly because of protection level in Coop.Core
        int interactionInt = (int)interaction;

        if (CaravansPlayerData.PlayerInteractedCaravans[playerHeroId].ContainsKey(mobilePartyId))
        {
            CaravansPlayerData.PlayerInteractedCaravans[playerHeroId][mobilePartyId] = interactionInt;
            return;
        }
        CaravansPlayerData.PlayerInteractedCaravans[playerHeroId].Add(mobilePartyId, interactionInt);
    }

    public void RemoveInteractedCaravanForAllPlayers(string mobilePartyId)
    {
        foreach (var player in playerManager.Players)
        {
            string playerHeroId = player.HeroId;
            if (CaravansPlayerData.PlayerInteractedCaravans[playerHeroId].ContainsKey(mobilePartyId))
            {
                CaravansPlayerData.PlayerInteractedCaravans[playerHeroId].Remove(mobilePartyId);
            }
        }
    }

    public void UpdateTradeRumorTakenCaravansForPlayer(string playerHeroId, Dictionary<string, CampaignTime> tradeRumorTakenCaravansIds)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        CaravansPlayerData.PlayerTradeRumorTakenCaravans[playerHeroId] = tradeRumorTakenCaravansIds;
    }

    public void DeleteExpiredTradeRumorTakenCaravans(out Dictionary<string, List<string>> playerExpiredCaravansRemovalLists)
    {
        playerExpiredCaravansRemovalLists = new();
        foreach (var playerTradeRumourTakenCaravan in CaravansPlayerData.PlayerTradeRumorTakenCaravans)
        {
            if (playerTradeRumourTakenCaravan.Value == null) continue;

            var removalList = new List<string>();
            foreach (var tradeRumorTakenCaravan in playerTradeRumourTakenCaravan.Value)
            {
                if (CampaignTime.Now - tradeRumorTakenCaravan.Value >= CampaignTime.Days(1f))
                {
                    removalList.Add(tradeRumorTakenCaravan.Key);
                }
            }

            playerExpiredCaravansRemovalLists.Add(playerTradeRumourTakenCaravan.Key, removalList);

            foreach (string mobilePartyId in removalList)
            {
                CaravansPlayerData.PlayerTradeRumorTakenCaravans[playerTradeRumourTakenCaravan.Key].Remove(mobilePartyId);
            }
        }
    }

    public bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction, MobileParty mobileParty)
    {
        if (mobileParty == null || caravanFaction.IsAtWarWith(targetFaction))
            return false;

        // Allow AI caravans to trade as long as they are not at war with the target faction
        // Not all AI caravans are part of a clan and can just belong to a kingdom directly
        if (mobileParty.ActualClan == null || !mobileParty.ActualClan.IsPlayerClan())
            return true;

        // Allow player caravans to trade with non-kingdom factions (e.g. independent clans who own settlements)
        if (targetFaction is not Kingdom kingdom)
            return true;

        if (!objectManager.TryGetIdWithLogging(mobileParty.ActualClan.Leader, out var playerHeroId) || !IsPlayerHeroIdValid(playerHeroId)) return false;
        if (!objectManager.TryGetIdWithLogging(kingdom, out var kingdomId)) return false;

        // Prevent trading if the caravan's player owner has prohibited trading with this kingdom
        return !CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId].Contains(kingdomId);
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (CaravansPlayerData == null)
        {
            Logger.Error("CaravansPlayerData was null");
            return;
        }

        if (!CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans.ContainsKey(playerHeroId))
        {
            CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId] = new List<string>();
        }
        if (!CaravansPlayerData.PlayerInteractedCaravans.ContainsKey(playerHeroId))
        {
            CaravansPlayerData.PlayerInteractedCaravans[playerHeroId] = new Dictionary<string, int>();
        }
        if (!CaravansPlayerData.PlayerTradeRumorTakenCaravans.ContainsKey(playerHeroId))
        {
            CaravansPlayerData.PlayerTradeRumorTakenCaravans[playerHeroId] = new Dictionary<string, CampaignTime>();
        }
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging(playerHeroId, out Hero _);
    }
}
