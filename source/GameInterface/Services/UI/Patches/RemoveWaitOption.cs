using HarmonyLib;
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
    [HarmonyPatch(nameof(GameMenu.SwitchToMenu))]
    static bool Prefix(string menuId)
    {
        MenuContext currentMenuContext = Campaign.Current.CurrentMenuContext;
        if (currentMenuContext != null)
        {
            currentMenuContext.SwitchToMenu(menuId);
            if (currentMenuContext.GameMenu.IsWaitMenu)
            {
                currentMenuContext.GameMenu.StartWait();
                return false;
            }
        }

        return false;
    }
}
