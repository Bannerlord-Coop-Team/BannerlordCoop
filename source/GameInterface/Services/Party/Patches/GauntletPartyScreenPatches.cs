using Common;
using HarmonyLib;
using SandBox.GauntletUI;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch]
internal class GauntletPartyScreenPatches
{
    [HarmonyPatch(typeof(GauntletPartyScreen), nameof(GauntletPartyScreen.OnResume))]
    [HarmonyPrefix]
    public static void OnResumePrefix(GauntletPartyScreen __instance)
    {
        if (ModInformation.IsServer) return;
        if (__instance._dataSource?.IsInConversation != true) return;

        // PartyScreenRosterRefresher kept the screen data current during the talk, but the rows
        // weren't on top to refresh.
        __instance._partyState.PartyScreenLogic.OnReset(false);
    }
}
