using SandBox.GauntletUI;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Inventory.Interfaces;

/// <summary>Shows received roster changes on the open inventory screen.</summary>
public interface IInventoryScreenViewInterface : IGameAbstraction
{
    /// <summary>Updates the open screen's row for <paramref name="element"/> to its current count on <paramref name="side"/>.</summary>
    void RefreshItem(InventoryLogic logic, InventoryLogic.InventorySide side, EquipmentElement element, int amount);
}

/// <summary>Refreshes single item rows of the open <see cref="SPInventoryVM"/> through its own transfer handling.</summary>
internal class InventoryScreenViewInterface : IInventoryScreenViewInterface
{
    /// <summary>Feeds the change to the view as a transfer result, the same way vanilla slaughter and donate do.</summary>
    public void RefreshItem(InventoryLogic logic, InventoryLogic.InventorySide side, EquipmentElement element, int amount)
    {
        var view = GetOpenView(logic);
        if (view == null) return;

        var roster = logic._rosters[(int)side];
        int index = roster.FindIndexOfElement(element);
        var current = index < 0 ? new ItemRosterElement(element, 0) : roster.GetElementCopyAtIndex(index);
        var result = new TransferCommandResult(side, current, amount, current.Amount, EquipmentIndex.None, null);

        view.IsRefreshed = false;
        view.AfterTransfer(logic, new List<TransferCommandResult> { result });
        view.RefreshInformationValues();
        if (current.Amount == 0) view.ExecuteRemoveZeroCounts();
        view.IsRefreshed = true;
    }

    /// <summary>Returns the view model of the inventory screen showing <paramref name="logic"/>, or null.</summary>
    private static SPInventoryVM GetOpenView(InventoryLogic logic)
    {
        var view = (ScreenManager.TopScreen as GauntletInventoryScreen)?._dataSource;
        return ReferenceEquals(view?._inventoryLogic, logic) ? view : null;
    }
}
