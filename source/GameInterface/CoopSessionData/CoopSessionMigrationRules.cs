using GameInterface.Services.Caravans;
using GameInterface.Services.Heroes;
using GameInterface.Services.Inventory;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.CoopSessionData;

internal static class CoopSessionMigrationRules
{
    public static readonly List<PropertyInfo> PreserveWithMigration = new();

    public static readonly List<PropertyInfo> ClearWithMigration = new();

    static CoopSessionMigrationRules()
    {
        /// Define fields that should transfer existing data to the new player hero id
        PreserveWithMigration.Add(AccessTools.Property(typeof(CraftingPlayerData), nameof(CraftingPlayerData.PlayerOpenNewPartXpDictionary)));
        PreserveWithMigration.Add(AccessTools.Property(typeof(CraftingPlayerData), nameof(CraftingPlayerData.PlayerOpenedPartsDictionary)));
        PreserveWithMigration.Add(AccessTools.Property(typeof(CraftingPlayerData), nameof(CraftingPlayerData.PlayerCraftedItemsHistory)));

        PreserveWithMigration.Add(AccessTools.Property(typeof(WorkshopPlayerData), nameof(WorkshopPlayerData.PlayerWarehouseRosterPerSettlement)));

        PreserveWithMigration.Add(AccessTools.Property(typeof(CaravansPlayerData), nameof(CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans)));

        PreserveWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerKnowTournaments)));
        PreserveWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerWarningTime)));

        PreserveWithMigration.Add(AccessTools.Property(typeof(TradePlayerData), nameof(TradePlayerData.PlayerItemsTradeData)));

        PreserveWithMigration.Add(AccessTools.Property(typeof(InventoryPlayerData), nameof(InventoryPlayerData.PlayerInventoryLocks)));
        PreserveWithMigration.Add(AccessTools.Property(typeof(InventoryPlayerData), nameof(InventoryPlayerData.PlayerInventorySortPreferences)));

        /// Define fields that should have their data cleared
        ClearWithMigration.Add(AccessTools.Property(typeof(CaravansPlayerData), nameof(CaravansPlayerData.PlayerTradeRumorTakenCaravans)));

        ClearWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerInteractedVillagers)));
        ClearWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerInteractedCaravans)));
        ClearWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerInteractedBandits)));
        ClearWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerInteractedPatrols)));
        ClearWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerMetArenaMasters)));
        ClearWithMigration.Add(AccessTools.Property(typeof(InteractionsPlayerData), nameof(InteractionsPlayerData.PlayerAlreadySneakedSettlements)));

        ClearWithMigration.Add(AccessTools.Property(typeof(TradePlayerData), nameof(TradePlayerData.PlayerTradeRumors)));
        ClearWithMigration.Add(AccessTools.Property(typeof(TradePlayerData), nameof(TradePlayerData.PlayerEnteredSettlements)));

        ClearWithMigration.Add(AccessTools.Property(typeof(HeroMeetingData), nameof(HeroMeetingData.PlayerLastMeetingTimes)));

        ClearWithMigration.Add(AccessTools.Property(typeof(AgingPlayerData), nameof(AgingPlayerData.PlayerIsIllDays)));
    }
}
