using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Workshops.Interfaces;

public interface ISessionWorkshopPlayerDataInterface : IGameAbstraction
{
    void AddNewWarehouseDataIfNeeded(string ownerId, string settlementId);
    void RemoveWarehouseData(string ownerId, string settlementId);
    void AddToWarehouse(string ownerId, string settlementId, EquipmentElement outputItem);
    void RemoveFromWarehouse(string ownerId, string settlementId, ItemObject itemAtIndex, int inputCount);
    List<ItemRosterElement> GetWarehouseRoster(string ownerId, string settlementId);
    void UpdateWarehouseRoster(string ownerId, string settlementId, ItemRosterElement[] newWarehouseData);
    ItemRosterElement GetItemRosterElementFromData(ItemRosterElementData itemRosterElementData);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionWorkshopPlayerDataInterface : ISessionWorkshopPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionWorkshopPlayerDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private WorkshopPlayerData WorkshopPlayerData => coopSessionProvider.CoopSession.WorkshopPlayerData;

    public SessionWorkshopPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    public void AddNewWarehouseDataIfNeeded(string ownerId, string settlementId)
    {
        bool existingData = false;
        foreach (var settlementWarehouseRoster in WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId])
        {
            if (settlementWarehouseRoster.Key == settlementId)
            {
                existingData = true;
                break;
            }
        }
        if (!existingData)
        {
            for (int j = 0; j < WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId].Length; j++)
            {
                var settlementWarehouseRoster = WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][j];
                if (settlementWarehouseRoster.Value == null)
                {
                    WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][j] = new KeyValuePair<string, List<ItemRosterElementData>>(settlementId, new List<ItemRosterElementData>());
                    return;
                }

            }
        }
    }

    public void RemoveWarehouseData(string ownerId, string settlementId)
    {
        for (int i = 0; i < WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId].Length; i++)
        {
            if (WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][i].Key == settlementId)
            {
                WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][i] = new KeyValuePair<string, List<ItemRosterElementData>>(null, null);
                return;
            }
        }
    }

    public void AddToWarehouse(string ownerId, string settlementId, EquipmentElement outputItem)
    {
        if (!objectManager.TryGetIdWithLogging(outputItem.Item, out var outputItemId)) return;

        string itemModifierId = null;
        if (outputItem.ItemModifier != null && !objectManager.TryGetIdWithLogging(outputItem.ItemModifier, out itemModifierId)) return;

        foreach (var settlementWarehouseRoster in WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId])
        {
            if (settlementWarehouseRoster.Key != settlementId) continue;

            for (int i = 0; i < settlementWarehouseRoster.Value.Count; i++)
            {
                var elementData = settlementWarehouseRoster.Value[i];

                if (elementData.ItemObjectData.ItemObjectId == outputItemId)
                {
                    // Update existing stored item roster element
                    elementData.Amount += 1;
                    settlementWarehouseRoster.Value[i] = elementData;
                    return;
                }
            }

            // No existing item roster element was found, add a new one
            settlementWarehouseRoster.Value.AddItem(new ItemRosterElementData(new(outputItemId, itemModifierId, itemModifierId == null), 1));
        }
    }

    public void RemoveFromWarehouse(string ownerId, string settlementId, ItemObject itemAtIndex, int inputCount)
    {
        foreach (var settlementWarehouseRoster in WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId])
        {
            if (settlementWarehouseRoster.Key != settlementId) continue;

            for (int i = 0; i < settlementWarehouseRoster.Value.Count; i++)
            {
                var elementData = settlementWarehouseRoster.Value[i];
                if (!objectManager.TryGetIdWithLogging(itemAtIndex, out var itemAtIndexId)) continue;

                if (elementData.ItemObjectData.ItemObjectId == itemAtIndexId)
                {
                    // Update existing stored item roster element
                    elementData.Amount -= inputCount;
                    settlementWarehouseRoster.Value[i] = elementData;
                    return;
                }
            }
        }
    }

    public List<ItemRosterElement> GetWarehouseRoster(string ownerId, string settlementId)
    {
        foreach (var settlementWarehouseRoster in WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId])
        {
            if (settlementWarehouseRoster.Key == settlementId)
            {
                ResolveWarehouseElements(settlementWarehouseRoster.Value, out var rosterElements);
                return rosterElements;
            }
        }
        return new(); // Empty list
    }

    public void UpdateWarehouseRoster(string ownerId, string settlementId, ItemRosterElement[] newWarehouseData)
    {
        var playerWarehouseRosters = WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId];
        for (int i = 0; i < playerWarehouseRosters.Length; i++)
        {
            if (playerWarehouseRosters[i].Key == settlementId)
            {
                playerWarehouseRosters[i].Value.Clear();
                foreach (var item in newWarehouseData)
                {
                    if (!TryResolveWarehouseElementIds(item, out var elementData)) continue;
                    playerWarehouseRosters[i].Value.Add(elementData);
                }
            }
        }
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (WorkshopPlayerData == null)
        {
            Logger.Error("WorkshopPlayerData was null");
            return;
        }

        if (!WorkshopPlayerData.PlayerWarehouseRosterPerSettlement.ContainsKey(playerHeroId))
        {
            WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[playerHeroId] = new KeyValuePair<string, List<ItemRosterElementData>>[Campaign.Current.Models.ClanTierModel.MaxClanTier + 1];
        }
    }

    public ItemRosterElement GetItemRosterElementFromData(ItemRosterElementData itemRosterElementData)
    {
        TryResolveWarehouseElement(itemRosterElementData, out var result);
        return result;
    }

    private bool TryResolveWarehouseElementIds(ItemRosterElement itemRosterElement, out ItemRosterElementData itemRosterElementData)
    {
        itemRosterElementData = new();

        if (!objectManager.TryGetIdWithLogging(itemRosterElement.EquipmentElement.Item, out var itemObjectId)) return false;

        string itemModifierId = null;
        bool itemModifierNull = itemRosterElement.EquipmentElement.ItemModifier == null;
        if (!itemModifierNull && !objectManager.TryGetIdWithLogging(itemRosterElement.EquipmentElement.ItemModifier, out itemModifierId)) return false;
        
        itemRosterElementData = new(new(itemObjectId, itemModifierId, itemModifierNull), itemRosterElement.Amount);
        return true;
    }

    private bool TryResolveWarehouseElement(ItemRosterElementData itemRosterElementData, out ItemRosterElement itemRosterElement)
    {
        itemRosterElement = new();

        if (!objectManager.TryGetObjectWithLogging<ItemObject>(itemRosterElementData.ItemObjectData.ItemObjectId, out var itemObject)) return false;

        ItemModifier itemModifier = null;
        if (!itemRosterElementData.ItemObjectData.ItemModifierNull && !objectManager.TryGetObjectWithLogging<ItemModifier>(itemRosterElementData.ItemObjectData.ItemModifierId, out itemModifier)) return false;

        itemRosterElement = new ItemRosterElement(itemObject, itemRosterElementData.Amount, itemModifier);
        return true;
    }

    private void ResolveWarehouseElements(List<ItemRosterElementData> serverWarehouseData, out List<ItemRosterElement> resolvedRosterElements)
    {
        resolvedRosterElements = new();
        foreach (var element in serverWarehouseData)
        {
            if (!TryResolveWarehouseElement(element, out var resolvedElement))
            {
                Logger.Warning($"Failed to resolve warehouse element for item {element.ItemObjectData.ItemObjectId}");
                continue;
            }
            resolvedRosterElements.Add(resolvedElement);
        }
    }
}
