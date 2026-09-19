using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.Multiplayer;

namespace GameInterface.Services.Chat.Patches;

[HarmonyPatch(typeof(MPChatVM))]
internal static class MPChatVMCoopPatches
{
    [HarmonyPatch(nameof(MPChatVM.IsChatAllowedByOptions))]
    [HarmonyPrefix]
    private static bool IsChatAllowedByOptionsPrefix(ref bool __result)
    {
        if (!ChatVanillaLogGate.IsActive) return true;

        __result = false;
        return false;
    }
}