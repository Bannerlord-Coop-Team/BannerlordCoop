using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using SandBox;
using SandBox.View.Map;
using Serilog;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(MapScreen))]
internal class GameLoadedPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SandBoxGameManager>();

    [HarmonyPostfix]
    [HarmonyPatch("OnActivate")]
    static void OnGameLoaded(ref MapScreen __instance)
    {
        MessageBroker.Instance.Publish(__instance, new CampaignLoaded());
    }
}
