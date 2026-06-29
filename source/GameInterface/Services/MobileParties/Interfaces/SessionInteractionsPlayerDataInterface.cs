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
    void RemoveInteractedVillagersForAllPlayers(string mobilePartyId);
    void RemoveInteractedCaravanForAllPlayers(string mobilePartyId);
    void RemoveInteractedBanditsForAllPlayers(string mobilePartyId);
    void RemoveInteractedPatrolForAllPlayers(string mobilePartyId);
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
        GameThread.RunSafe(() =>
        {
            if (!IsPlayerHeroIdValid(playerHeroId)) return;

            long numTicks = interactionTime._numTicks;

            if (InteractionsPlayerData.PlayerInteractedPatrols[playerHeroId].ContainsKey(settlementId))
            {
                InteractionsPlayerData.PlayerInteractedPatrols[playerHeroId][settlementId] = numTicks;
                return;
            }
            InteractionsPlayerData.PlayerInteractedPatrols[playerHeroId].Add(settlementId, numTicks);
        });
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

    public void RemoveInteractedPatrolForAllPlayers(string mobilePartyId)
    {
        foreach (var player in playerManager.Players)
        {
            string playerHeroId = player.HeroId;
            if (!InteractionsPlayerData.PlayerInteractedPatrols.ContainsKey(playerHeroId)) continue;

            if (InteractionsPlayerData.PlayerInteractedPatrols[playerHeroId].ContainsKey(mobilePartyId))
            {
                InteractionsPlayerData.PlayerInteractedPatrols[playerHeroId].Remove(mobilePartyId);
            }
        }
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
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging(playerHeroId, out Hero _);
    }
}
