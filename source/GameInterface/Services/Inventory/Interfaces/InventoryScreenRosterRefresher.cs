using Common.Util;
using GameInterface.Services.TownMarketDatas.Patches;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using SandBox.GauntletUI;

namespace GameInterface.Services.Inventory.Interfaces;

public interface IInventoryScreenRosterRefresher : IGameAbstraction
{
    void CaptureInventoryBaseline(InventoryLogic logic);
    ItemRosterElement[] GetInventoryBaseline(InventoryLogic logic);
    bool TryApplyInventoryUpdate(ItemRoster roster, EquipmentElement element, int amount);
    bool TryClearInventory(ItemRoster roster);
}

internal class InventoryScreenRosterRefresher : IInventoryScreenRosterRefresher
{
    private const string PendingChangesResetMessage = "Inventory updated. Pending changes reset.";
    private readonly ConditionalWeakTable<InventoryLogic, InventoryBaseline> inventoryBaselines = new();

    public void CaptureInventoryBaseline(InventoryLogic logic)
    {
        inventoryBaselines.Remove(logic);
        inventoryBaselines.Add(logic, new InventoryBaseline(logic));
    }

    public ItemRosterElement[] GetInventoryBaseline(InventoryLogic logic)
        => inventoryBaselines.TryGetValue(logic, out var baseline) ? baseline.Rosters[1].ToArray() : null;

    public bool TryApplyInventoryUpdate(ItemRoster roster, EquipmentElement element, int amount)
        => TryApplyInventoryUpdate(
            (GameStateManager.Current?.ActiveState as InventoryState)?.InventoryLogic,
            roster,
            element,
            amount);

    internal bool TryApplyInventoryUpdate(InventoryLogic logic, ItemRoster roster,
        EquipmentElement element, int amount)
    {
        if (logic == null || !inventoryBaselines.TryGetValue(logic, out var baseline)) return false;
        int side = Array.IndexOf(logic._rosters, roster);
        // Food upkeep changes the player's roster. Let market-side updates use the
        // ordinary roster path so another player's open trade is not reset.
        if (side != 1) return false;

        using (new AllowedThread())
        using (TownMarketDataPatches.SuppressReceivedRosterUpdate())
        {
            int index = roster.FindIndexOfElement(element);
            int available = index < 0 ? 0 : roster.GetElementNumber(index);
            int backupIndex = logic._rostersBackup[side].FindIndexOfElement(element);
            int backupAvailable = backupIndex < 0 ? 0 : logic._rostersBackup[side].GetElementNumber(backupIndex);
            if ((long)available + amount < 0 || (long)backupAvailable + amount < 0)
            {
                RestoreInventoryBackup(logic);
                logic.Reset(false);
                MBInformationManager.AddQuickInformation(new TextObject(PendingChangesResetMessage));
            }

            baseline.Rosters[side].AddToCounts(element, amount);
            logic._rostersBackup[side].AddToCounts(element, amount);
            roster.AddToCounts(element, amount);
            RefreshInventory(logic);
        }
        return true;
    }

    public bool TryClearInventory(ItemRoster roster)
    {
        var logic = (GameStateManager.Current?.ActiveState as InventoryState)?.InventoryLogic;
        if (logic == null || !inventoryBaselines.TryGetValue(logic, out var baseline)) return false;
        int side = Array.IndexOf(logic._rosters, roster);
        if (side != 1) return false;

        using (new AllowedThread())
        using (TownMarketDataPatches.SuppressReceivedRosterUpdate())
        {
            RestoreInventoryBackup(logic);
            logic.Reset(false);
            baseline.Rosters[side].Clear();
            logic._rostersBackup[side].Clear();
            roster.Clear();
            RefreshInventory(logic);
        }
        return true;
    }

    internal void RestoreInventoryBackup(InventoryLogic logic)
    {
        if (!inventoryBaselines.TryGetValue(logic, out var baseline)) return;
        // Slaughter refreshes rosters and equipment together; rollback must do the same.
        for (int side = 0; side < baseline.Rosters.Length; side++)
            logic._rostersBackup[side] = new ItemRoster(baseline.Rosters[side]);
        logic._partyInitialEquipment = baseline.PlayerEquipment;
        logic._otherPartyInitialEquipment = baseline.OtherPartyEquipment;
    }

    private sealed class InventoryBaseline
    {
        public readonly ItemRoster[] Rosters;
        public readonly InventoryLogic.PartyEquipment PlayerEquipment;
        public readonly InventoryLogic.PartyEquipment OtherPartyEquipment;

        public InventoryBaseline(InventoryLogic logic)
        {
            Rosters = logic._rosters.Select(roster => new ItemRoster(roster)).ToArray();
            PlayerEquipment = logic._partyInitialEquipment;
            OtherPartyEquipment = logic._otherPartyInitialEquipment;
        }
    }

    private static void RefreshInventory(InventoryLogic logic)
    {
        if (Game.Current == null) return;
        var view = (ScreenManager.TopScreen as GauntletInventoryScreen)?._dataSource;
        if (view != null && ReferenceEquals(view._inventoryLogic, logic))
            view.AfterReset(logic, false);
    }
}
