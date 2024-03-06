using HarmonyLib;
using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// This is used to show loading menu until full transfer of data loaded.
/// Is Done loading is Changed by LoadingScreenhandler
/// </summary>
[HarmonyPatch(typeof(LoadingWindow))]
internal class ShowMenuUntilLoadedPatch
{
    public static bool IsDoneLoading = false;
    [HarmonyPatch("DisableGlobalLoadingWindow")]
    [HarmonyPrefix]
    public static bool PushStatePatch()
    {
        if (ModInformation.IsServer) return true;

        if(IsDoneLoading) return true;
        return false;
    }
}
