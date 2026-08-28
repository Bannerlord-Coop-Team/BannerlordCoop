using Common.Messaging;
using GameInterface.Services.CampaignService.Handlers;
using GameInterface.Services.Players.Messages;
using HarmonyLib;
using SandBox.GauntletUI;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(GauntletGameOverScreen))]
internal class GauntletGameOverScreenPatches
{
    [HarmonyPatch(nameof(GauntletGameOverScreen.CloseGameOverScreen))]
    [HarmonyPrefix]
    public static bool CloseGameOverScreenPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(GauntletGameOverScreen.CloseGameOverScreen))]
    [HarmonyPostfix]
    public static void CloseGameOverScreenPostfix()
    {
        GameOverState.IsGameOver = false;
        MessageBroker.Instance.Publish(null, new PlayerDeleteRequested());
    }
}