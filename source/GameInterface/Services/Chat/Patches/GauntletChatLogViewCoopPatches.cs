using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.Chat.Patches;

[HarmonyPatch(typeof(GauntletChatLogView))]
internal static class GauntletChatLogViewCoopPatches
{
    [HarmonyPatch(nameof(GauntletChatLogView.Initialize))]
    [HarmonyPostfix]
    private static void InitializePostfix()
    {
        ChatVanillaLogGate.SuspendIfPresent();
    }
}
