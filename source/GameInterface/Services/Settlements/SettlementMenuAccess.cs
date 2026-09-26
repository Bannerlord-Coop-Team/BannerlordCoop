using Common.Network;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI.Menu;
using SandBox.View.Map;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Settlements;

public interface ISettlementMenuAccess : IGameAbstraction
{
    SettlementMenuUse[] GetOpenMenus();
    bool TryAcquire(string settlementId, string menuId, string heroId);
    bool Release(string heroId);
    void Update(SettlementMenuUse[] menus);
    bool IsInUse(Settlement settlement, string menuId);
    bool TryOpen(GameMenuOption option, MenuContext context);
    void OnAccessReceived(NetworkSettlementMenuAccess response);
    void Close(string menuId);
}

internal class SettlementMenuAccess : ISettlementMenuAccess
{
    private readonly Dictionary<string, SettlementMenuUse> openMenus = new();
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private GameMenuOption pendingOption;
    private MenuContext pendingContext;
    private string pendingSettlementId;
    private SettlementMenuUse? activeMenu;
    private bool opening;

    public SettlementMenuAccess(INetwork network, IObjectManager objectManager)
    {
        this.network = network;
        this.objectManager = objectManager;
    }

    public static bool IsManagedMenu(string menuId)
        => menuId == "manage_garrison" || menuId == "town_prison_manage_prisoners" ||
            menuId == "open_stash" || menuId == "manage_production";

    public static bool CanUseSettlement(Hero hero, Settlement settlement)
        => hero != null && settlement != null && hero.IsAlive && !hero.IsPrisoner &&
            hero.CurrentSettlement == settlement && hero.Clan == settlement.OwnerClan;

    public SettlementMenuUse[] GetOpenMenus() => openMenus.Values.ToArray();

    public bool TryAcquire(string settlementId, string menuId, string heroId)
    {
        if (IsInUse(settlementId, menuId)) return false;

        openMenus[heroId] = new SettlementMenuUse(settlementId, menuId, heroId);
        return true;
    }

    public bool Release(string heroId) => openMenus.Remove(heroId);

    public bool IsInUse(string settlementId, string menuId)
        => openMenus.Values.Any(menu => menu.SettlementId == settlementId && menu.MenuId == menuId);

    public bool IsInUse(Settlement settlement, string menuId)
        => !opening && settlement != null && objectManager.TryGetId(settlement, out var settlementId) && IsInUse(settlementId, menuId);

    public void Update(SettlementMenuUse[] menus)
    {
        openMenus.Clear();
        foreach (var menu in menus ?? Array.Empty<SettlementMenuUse>()) openMenus[menu.HeroId] = menu;

        RefreshMenu();
    }

    public bool TryOpen(GameMenuOption option, MenuContext context)
    {
        if (opening) return true;
        if (pendingOption != null || activeMenu != null) return false;
        if (!objectManager.TryGetIdWithLogging(Settlement.CurrentSettlement, out var settlementId)) return false;

        pendingOption = option;
        pendingContext = context;
        pendingSettlementId = settlementId;
        network.SendAll(new RequestSettlementMenuAccess(settlementId, option.IdString, true));
        return false;
    }

    public void OnAccessReceived(NetworkSettlementMenuAccess response)
    {
        if (pendingOption?.IdString != response.MenuId || pendingSettlementId != response.SettlementId) return;

        var option = pendingOption;
        var context = pendingContext;
        pendingOption = null;
        pendingContext = null;
        pendingSettlementId = null;
        if (!response.Granted)
        {
            InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_coop_clan_settlement_menu_in_use").ToString()));
            return;
        }

        bool opened = false;
        try
        {
            if (Campaign.Current?.CurrentMenuContext != context ||
                !(Game.Current.GameStateManager.ActiveState is MapState) ||
                !objectManager.TryGetObjectWithLogging<Settlement>(response.SettlementId, out var settlement) ||
                Settlement.CurrentSettlement != settlement) return;

            // Recheck access without treating this client's own reservation as a blocker.
            opening = true;
            if (!option.GetConditionsHold(Game.Current, context) || !option.IsEnabled) return;

            activeMenu = new SettlementMenuUse(response.SettlementId, response.MenuId, null);
            option.RunConsequence(context);
            opened = true;
        }
        finally
        {
            opening = false;
            if (!opened)
            {
                activeMenu = null;
                network.SendAll(new RequestSettlementMenuAccess(response.SettlementId, response.MenuId, false));
            }
        }
    }

    public void Close(string menuId)
    {
        if (activeMenu?.MenuId != menuId) return;

        network.SendAll(new RequestSettlementMenuAccess(activeMenu.Value.SettlementId, activeMenu.Value.MenuId, false));
        activeMenu = null;
        RefreshMenu();
    }

    private static void RefreshMenu()
    {
        var context = Campaign.Current?.CurrentMenuContext;
        if (context?.GameMenu == null) return;

        Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(context);
        MapScreen.Instance?._menuViewContext?.GetMenuView<GauntletMenuBaseView>()?.GameMenuDataSource.Refresh(true);
    }
}
