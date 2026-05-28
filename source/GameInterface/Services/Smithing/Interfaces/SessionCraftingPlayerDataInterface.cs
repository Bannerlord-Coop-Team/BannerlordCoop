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
    private CraftingPlayerData CraftingPlayerData => coopSessionProvider.CoopSession.CraftingPlayerData;

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
            CraftingPlayerData.PlayerOpenNewPartXpDictionary[playerHeroId][craftingTemplateId] = xp;
        }
    }

    public void UnlockCraftingPiece(string playerHeroId, string craftingTemplateId, string craftingPieceId)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            CraftingPlayerData.PlayerOpenedPartsDictionary[playerHeroId][craftingTemplateId].Add(craftingPieceId);
        }
    }

    public void UpdateCraftingHistory(string playerHeroId, List<string> craftedItemHistoryIds)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            CraftingPlayerData.PlayerCraftedItemsHistory[playerHeroId] = craftedItemHistoryIds;
        }
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (CraftingPlayerData == null)
        {
            Logger.Error("CraftingPlayerData was null");
            return;
        }

        if (!CraftingPlayerData.PlayerOpenNewPartXpDictionary.ContainsKey(playerHeroId))
        {
            CraftingPlayerData.PlayerOpenNewPartXpDictionary[playerHeroId] = new Dictionary<string, float>();
        }
        if (!CraftingPlayerData.PlayerOpenedPartsDictionary.ContainsKey(playerHeroId))
        {
            CraftingPlayerData.PlayerOpenedPartsDictionary[playerHeroId] = new Dictionary<string, List<string>>();
        }

        foreach (CraftingTemplate craftingTemplate in CraftingTemplate.All)
        {
            if (!objectManager.TryGetIdWithLogging(craftingTemplate, out string craftingTemplateId)) return;

            if (!CraftingPlayerData.PlayerOpenedPartsDictionary[playerHeroId].ContainsKey(craftingTemplateId))
            {
                CraftingPlayerData.PlayerOpenedPartsDictionary[playerHeroId][craftingTemplateId] = new List<string>();
            }
        }
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging(playerHeroId, out Hero _);
    }
}
