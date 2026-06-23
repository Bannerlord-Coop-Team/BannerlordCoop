using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Caravans.Interfaces;

public interface ISessionCaravansPlayerDataInterface : IGameAbstraction
{
    void AddProhibitedKingdom(string playerHeroId, string kingdomId);
    void RemoveProhibitedKingdom(string playerHeroId, string kingdomId);
    void RemoveProhibitedKingdomForAllPlayers(string kingdomId);
    void SetPlayerInteraction(string playerHeroId, string mobilePartyId, CaravansCampaignBehavior.PlayerInteraction interaction);
    void RemoveInteractedCaravanForAllPlayers(string mobilePartyId);
    void UpdateTradeRumorTakenCaravansForPlayer(string playerHeroId, Dictionary<string, CampaignTime> tradeRumorTakenCaravansIds);
    bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction);
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

        // Cast to save, can't use enumerable directly because of protection level
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

    public bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction)
    {
        // TODO: Implement this properly

        /*
        bool isPlayerFaction = false;
        foreach (var player in playerManager.Players)
        {
            
        }

        Kingdom item;
        return !caravanFaction.IsAtWarWith(targetFaction)
            && (caravanFaction != Hero.MainHero.MapFaction || (item = (targetFaction as Kingdom)) == null || !this._prohibitedKingdomsForPlayerCaravans.Contains(item));
        */
        return false;
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
