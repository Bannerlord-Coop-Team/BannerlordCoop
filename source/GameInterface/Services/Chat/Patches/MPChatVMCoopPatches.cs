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
        // Only block vanilla while the co-op overlay is actually on screen.
        // Otherwise menus (party, options, etc.) get a resumed layer that still never shows.
        if (!ChatVanillaLogGate.IsReplacementVisible) return true;

        __result = false;
        return false;
    }
}
