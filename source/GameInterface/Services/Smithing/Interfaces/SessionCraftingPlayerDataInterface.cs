using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Interfaces;

public interface ISessionCraftingPlayerDataInterface : IGameAbstraction
{
    void SetCraftingPieceXp(string playerHeroId, string craftingTemplateId, float xp);
    float GetCraftingPieceXp(string playerHeroId, string craftingTemplateId);
    IReadOnlyCollection<string> GetOpenedCraftingPieces(
        string playerHeroId, string craftingTemplateId);
    void UnlockCraftingPiece(string playerHeroId, string craftingTemplateId, string craftingPieceId);
    void UpdateCraftingHistory(string playerHeroId, List<string> craftedItemHistoryIds);
    IReadOnlyList<string> AppendCraftingHistory(
        string playerHeroId, string craftedItemId);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionCraftingPlayerDataInterface : ISessionCraftingPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionCraftingPlayerDataInterface>();
    private ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private CraftingPlayerData CraftingPlayerData => coopSessionProvider.CoopSession.CraftingPlayerData;

    public SessionCraftingPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
    }

    public void SetCraftingPieceXp(string playerHeroId, string craftingTemplateId, float xp)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            CraftingPlayerData.PlayerOpenNewPartXpDictionary[playerHeroId][craftingTemplateId] = xp;
        }
    }

    public float GetCraftingPieceXp(
        string playerHeroId,
        string craftingTemplateId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId) ||
            !CraftingPlayerData.PlayerOpenNewPartXpDictionary
                .TryGetValue(playerHeroId, out var templates) ||
            !templates.TryGetValue(craftingTemplateId, out float xp))
            return 0f;
        return xp;
    }

    public IReadOnlyCollection<string> GetOpenedCraftingPieces(
        string playerHeroId,
        string craftingTemplateId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId) ||
            !CraftingPlayerData.PlayerOpenedPartsDictionary
                .TryGetValue(playerHeroId, out var templates) ||
            !templates.TryGetValue(craftingTemplateId, out var pieces) ||
            pieces == null)
            return Array.Empty<string>();
        return pieces.ToArray();
    }

    public void UnlockCraftingPiece(string playerHeroId, string craftingTemplateId, string craftingPieceId)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            List<string> pieces = CraftingPlayerData
                .PlayerOpenedPartsDictionary[playerHeroId][craftingTemplateId];
            if (!pieces.Contains(craftingPieceId))
                pieces.Add(craftingPieceId);
        }
    }

    public void UpdateCraftingHistory(string playerHeroId, List<string> craftedItemHistoryIds)
    {
        if (IsPlayerHeroIdValid(playerHeroId))
        {
            List<string> cleaned =
                (craftedItemHistoryIds ?? new List<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
            if (cleaned.Count > 200)
                cleaned.RemoveRange(0, cleaned.Count - 200);
            CraftingPlayerData.PlayerCraftedItemsHistory[playerHeroId] = cleaned;
        }
    }

    public IReadOnlyList<string> AppendCraftingHistory(
        string playerHeroId, string craftedItemId)
    {
        if (!IsPlayerHeroIdValid(playerHeroId) ||
            string.IsNullOrEmpty(craftedItemId))
            return Array.Empty<string>();
        if (!CraftingPlayerData.PlayerCraftedItemsHistory.TryGetValue(
                playerHeroId, out List<string> history) || history == null)
        {
            history = new List<string>();
            CraftingPlayerData.PlayerCraftedItemsHistory[playerHeroId] = history;
        }
        history.RemoveAll(id => string.Equals(
            id, craftedItemId, StringComparison.Ordinal));
        history.Add(craftedItemId);
        if (history.Count > 200)
            history.RemoveRange(0, history.Count - 200);
        return history.ToArray();
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
        if (!CraftingPlayerData.PlayerCraftedItemsHistory.ContainsKey(playerHeroId))
            CraftingPlayerData.PlayerCraftedItemsHistory[playerHeroId] =
                new List<string>();

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
