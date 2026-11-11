using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Inventory.Interfaces;

public interface ISessionInventoryPlayerDataInterface : IGameAbstraction
{
    void SetInventoryLocks(string playerHeroId, IEnumerable<string> lockedItemIds);
    IReadOnlyCollection<string> GetInventoryLocks(string playerHeroId);
    void SetSortPreference(string playerHeroId, int usageType, Tuple<int, int> preference);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionInventoryPlayerDataInterface : ISessionInventoryPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionInventoryPlayerDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;

    private InventoryPlayerData InventoryPlayerData => coopSessionProvider.CoopSession.InventoryPlayerData;

    public SessionInventoryPlayerDataInterface(ICoopSessionProvider coopSessionProvider)
    {
        this.coopSessionProvider = coopSessionProvider;
    }

    public void SetInventoryLocks(string playerHeroId, IEnumerable<string> lockedItemIds)
    {
        if (!InventoryPlayerData.PlayerInventoryLocks.ContainsKey(playerHeroId)) return;

        InventoryPlayerData.PlayerInventoryLocks[playerHeroId] = (List<string>)lockedItemIds;
    }

    public IReadOnlyCollection<string> GetInventoryLocks(string playerHeroId)
    {
        // An empty lock list comes back from the session data as null.
        if (InventoryPlayerData?.PlayerInventoryLocks == null ||
            !InventoryPlayerData.PlayerInventoryLocks.TryGetValue(playerHeroId, out var locks) ||
            locks == null)
            return Array.Empty<string>();

        return locks;
    }

    public void SetSortPreference(string playerHeroId, int usageType, Tuple<int, int> preference)
    {
        if (!InventoryPlayerData.PlayerInventorySortPreferences.ContainsKey(playerHeroId)) return;

        InventoryPlayerData.PlayerInventorySortPreferences[playerHeroId][usageType] = preference;
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (InventoryPlayerData == null)
        {
            Logger.Error("InventoryPlayerData was null");
            return;
        }

        if (!InventoryPlayerData.PlayerInventoryLocks.ContainsKey(playerHeroId))
        {
            InventoryPlayerData.PlayerInventoryLocks[playerHeroId] = new();
        }
        if (!InventoryPlayerData.PlayerInventorySortPreferences.ContainsKey(playerHeroId))
        {
            InventoryPlayerData.PlayerInventorySortPreferences[playerHeroId] = new();
        }
    }
}
