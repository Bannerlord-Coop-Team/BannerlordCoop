using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using SandBox;
using SandBox.View.Map;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(MBGameManager))]
internal class GameLoadedPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameLoadedPatch>();
    [HarmonyPostfix]
    [HarmonyPatch("OnAfterGameInitializationFinished")]
    static void OnGameLoaded(ref MBGameManager __instance)
    {
        try
        {
            if (Game.Current?.GameStateManager != null && MapScreen.Instance != null)
            {
                Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(MapScreen.Instance);
            }
        }
        catch { }

        Logger.Information("Publishing CampaignReady (OnAfterGameInitializationFinished)");
        MessageBroker.Instance.Publish(__instance, new CampaignReady());
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnGameLoaded")]
    static void OnGameLoadedAlt(ref MBGameManager __instance)
    {
        Logger.Information("Publishing CampaignReady (OnGameLoaded)");
        MessageBroker.Instance.Publish(__instance, new CampaignReady());
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnGameInitializationFinished")]
    static void OnGameInitializationFinished(ref MBGameManager __instance)
    {
        Logger.Information("Publishing CampaignReady (OnGameInitializationFinished)");
        MessageBroker.Instance.Publish(__instance, new CampaignReady());
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnGameStart")]
    static void OnGameStart(ref MBGameManager __instance)
    {
        // Publication supprimée: trop tôt dans le cycle d'initialisation et provoque des crashes
    }
}
