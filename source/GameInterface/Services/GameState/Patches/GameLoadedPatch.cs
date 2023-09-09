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
    [HarmonyPostfix]
    [HarmonyPatch("OnAfterGameInitializationFinished")]
    static void OnGameLoaded(ref MBGameManager __instance)
    {
        MessageBroker.Instance.Publish(__instance, new CampaignReady());
    }
}
