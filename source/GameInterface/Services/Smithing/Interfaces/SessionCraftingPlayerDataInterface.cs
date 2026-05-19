using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Interfaces;

public interface ISessionCraftingPlayerDataInterface : IGameAbstraction
{
    void SetCraftingPieceXp(string playerHeroId, string craftingTemplateId, float xp);
    void UnlockCraftingPiece(string playerHeroId, string craftingTemplateId, string craftingPieceId);
    void UpdateCraftingHistory(string playerHeroId, List<string> craftedItemHistoryIds);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionCraftingPlayerDataInterface : ISessionCraftingPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionCraftingPlayerDataInterface>();
    private ICoopSessionProvider coopSessionProvider;
    private readonly IPlayerRegistry playerRegistry;
    private readonly IObjectManager objectManager;

    public SessionCraftingPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IPlayerRegistry playerRegistry, IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.playerRegistry = playerRegistry;
        this.objectManager = objectManager;
    }

    public void SetCraftingPieceXp(string playerHeroId, string craftingTemplateId, float xp)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            coopSessionProvider.CoopSession.CraftingPlayerData.PlayerOpenNewPartXpDictionary[playerHeroId][craftingTemplateId] = xp;
        }
    }

    public void UnlockCraftingPiece(string playerHeroId, string craftingTemplateId, string craftingPieceId)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            coopSessionProvider.CoopSession.CraftingPlayerData.PlayerOpenedPartsDictionary[playerHeroId][craftingTemplateId].Add(craftingPieceId);
        }
    }

    public void UpdateCraftingHistory(string playerHeroId, List<string> craftedItemHistoryIds)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            coopSessionProvider.CoopSession.CraftingPlayerData.PlayerCraftedItemsHistory[playerHeroId] = craftedItemHistoryIds;
        }
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        CraftingPlayerData craftingData = coopSessionProvider.CoopSession.CraftingPlayerData;

        if (craftingData == null)
        {
            Logger.Error("CraftingPlayerData was null");
            return;
        }

        if (!craftingData.PlayerOpenNewPartXpDictionary.ContainsKey(playerHeroId))
        {
            craftingData.PlayerOpenNewPartXpDictionary[playerHeroId] = new Dictionary<string, float>();
        }
        if (!craftingData.PlayerOpenedPartsDictionary.ContainsKey(playerHeroId))
        {
            craftingData.PlayerOpenedPartsDictionary[playerHeroId] = new Dictionary<string, List<string>>();
        }

        foreach (CraftingTemplate craftingTemplate in CraftingTemplate.All)
        {
            if (!objectManager.TryGetIdWithLogging(craftingTemplate, out string craftingTemplateId)) return;

            if (!craftingData.PlayerOpenedPartsDictionary[playerHeroId].ContainsKey(craftingTemplateId))
            {
                craftingData.PlayerOpenedPartsDictionary[playerHeroId][craftingTemplateId] = new List<string>();
            }
        }
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        if (!objectManager.TryGetObject(playerHeroId, out Hero _))
        {
            Logger.Error("Unable find playerHero object for id {id}", playerHeroId);
            return false;
        }

        return true;
    }

    // PlayerRegistry doesn't save so when resuming a save with the same players it isn't populated. Until it is, will just have to use hero ids as keys for CraftingPlayerData
    private bool GetPlayerFromHeroId(string playerHeroId, out Player targetPlayer)
    {
        targetPlayer = null;
        foreach (Player player in playerRegistry)
        {
            if (player.HeroStringId == playerHeroId)
            {
                targetPlayer = player;
                return true;
            }
        }

        Logger.Error("Unable to get Player from playerHeroId");
        return false;
    }
}
