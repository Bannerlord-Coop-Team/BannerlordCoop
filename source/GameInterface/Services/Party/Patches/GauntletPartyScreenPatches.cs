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

        // PartyScreenRosterRefresher kept the screen data current during the talk while the rows were
        // hidden, and the Done Reset(true) on resume skips rebuilding them, so rebuild them here.
        var partyScreenLogic = __instance._partyState.PartyScreenLogic;
        if (ContainerProvider.TryResolve<IPartyScreenLockPreserver>(out var lockPreserver))
            lockPreserver.Reset(partyScreenLogic, () => partyScreenLogic.OnReset(false));
        else
            partyScreenLogic.OnReset(false);
    }
}
