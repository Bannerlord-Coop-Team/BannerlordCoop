using HarmonyLib;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(SandBox.View.Menu.MenuViewContext))]
    public class MenuViewContextOverlayFinalizer
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MenuViewContextOverlayFinalizer>();
        [HarmonyFinalizer]
        [HarmonyPatch("CheckAndInitializeOverlay")]
        public static System.Exception Finalizer(System.Exception __exception)
        {
            if (__exception != null)
            {
                Logger.Error(__exception, "Overlay init crash évité");
                return null;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay.GameMenuOverlayFactory))]
    public class GameMenuOverlayFactoryFinalizer
    {
        private static readonly ILogger Logger = LogManager.GetLogger<GameMenuOverlayFactoryFinalizer>();
        [HarmonyFinalizer]
        [HarmonyPatch("GetOverlay")]
        public static System.Exception Finalizer(System.Exception __exception)
        {
            if (__exception != null)
            {
                Logger.Error(__exception, "Overlay factory crash évité");
                return null;
            }
            return null;
        }
    }
}

[HarmonyPatch(typeof(SettlementMenuOverlayVM))]
public class SettlementMenuOverlayVMFinalizer
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementMenuOverlayVMFinalizer>();

    [HarmonyFinalizer]
    [HarmonyPatch("UpdateProperties")]
    public static System.Exception UpdatePropertiesFinalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "SettlementMenuOverlayVM.UpdateProperties crash évité");
            return null;
        }
        return null;
    }

    [HarmonyFinalizer]
    [HarmonyPatch("Refresh")]
    public static System.Exception RefreshFinalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "SettlementMenuOverlayVM.Refresh crash évité");
            return null;
        }
        return null;
    }
}
