using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
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
