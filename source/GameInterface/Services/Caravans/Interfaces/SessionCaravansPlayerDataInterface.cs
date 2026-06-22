using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
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
    void AddPlayerKeys(string playerHeroId);
}

public class SessionCaravansPlayerDataInterface : ISessionCaravansPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionCaravansPlayerDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private CaravansPlayerData CaravansPlayerData => coopSessionProvider.CoopSession.CaravansPlayerData;

    public SessionCaravansPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
    }

    public void AddProhibitedKingdom(string playerHeroId, string kingdomId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        // TODO: ADD LOGIC
    }

    public void RemoveProhibitedKingdom(string playerHeroId, string kingdomId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        // TODO: ADD LOGIC
    }

    public void RemoveProhibitedKingdomForAllPlayers(string kingdomId)
    {
        // Should be called on a CaravansCampaignBehavior.OnKingdomDestroyed patch
        // TODO: ADD LOGIC
    }

    public void SetPlayerInteraction(string playerHeroId, string mobilePartyId, CaravansCampaignBehavior.PlayerInteraction interaction)
    {
        if (!IsPlayerHeroIdValid(playerHeroId)) return;

        // Cast to save, can't use enumerable directly because of protection level
        int interactionInt = (int)interaction;

        // TODO: ADD LOGIC
    }

    public void RemoveInteractedCaravanForAllPlayers(string mobilePartyId)
    {
        // TODO: ADD LOGIC
        /*
        foreach (var player in )
        {
            if (this._interactedCaravans.ContainsKey(mobileParty))
            {
                this._interactedCaravans.Remove(mobileParty);
            }
        }
        */
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
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging(playerHeroId, out Hero _);
    }
}
