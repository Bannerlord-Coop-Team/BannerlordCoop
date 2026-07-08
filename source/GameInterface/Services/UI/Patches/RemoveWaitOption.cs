using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(GameMenu))]
internal class RemoveWaitOption
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemoveWaitOption>();

    [HarmonyPatch(nameof(GameMenu.SwitchToMenu))]
    static bool Prefix(string menuId)
    {
        // TEMP [MenuDiag]: trace which caller switches menus after a siege capture (the aftermath menu is
        // being replaced by the normal settlement menu).
        Logger.Information("[MenuDiag] SwitchToMenu -> {MenuId} (from {Caller})", menuId,
            new System.Diagnostics.StackTrace().GetFrame(2)?.GetMethod()?.Name ?? "?");

        MenuContext currentMenuContext = Campaign.Current.CurrentMenuContext;
        if (currentMenuContext != null)
        {
            try
            {
                currentMenuContext.SwitchToMenu(menuId);
                if (currentMenuContext.GameMenu.IsWaitMenu)
                {
                    currentMenuContext.GameMenu.StartWait();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to switch to menu {menuId}");
                GameMenu.ExitToLast();
            }
        }

        return false;
    }
}
