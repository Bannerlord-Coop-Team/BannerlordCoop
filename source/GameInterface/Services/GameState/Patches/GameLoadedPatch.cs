using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using SandBox;
using SandBox.View.Map;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class GameLoadedPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SandBoxGameManager>();

    [HarmonyPostfix]
    [HarmonyPatch("OnSessionStart")]
    static void OnGameLoaded(ref MapScreen __instance)
    {
        MessageBroker.Instance.Publish(__instance, new CampaignReady());
    }
}
