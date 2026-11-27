using Common.Logging;
using HarmonyLib;
using SandBox.View.Menu;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.GameMenus.Patches;

/// <summary>
/// Releases the menu views a failed <see cref="MenuViewContext"/> constructor already built.
/// </summary>
[HarmonyPatch(typeof(MenuViewContext), MethodType.Constructor, typeof(ScreenBase), typeof(MenuContext))]
internal class MenuViewContextPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MenuViewContextPatches>();

    // ScreenManagerRobustnessPatches swallows this throw, so MapScreen retries the menu every frame and each leaked view stacks another panel.
    [HarmonyFinalizer]
    private static void Finalizer(Exception __exception, MenuViewContext __instance, MenuContext menuContext)
    {
        if (__exception == null) return;

        foreach (var menuView in __instance.MenuViews.ToArray())
        {
            try
            {
                __instance.RemoveMenuView(menuView);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to release {View} of a menu that failed to open", menuView.GetType().Name);
            }
        }

        if (menuContext?.Handler == __instance) menuContext.Handler = null;
    }
}
