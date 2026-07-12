using Common;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Applies the refreshed main-hero portrait that vanilla stores without rebinding to the nameplate.
/// </summary>
[HarmonyPatch(typeof(PartyPlayerNameplateVM), nameof(PartyPlayerNameplateVM.RefreshBinding))]
internal class PartyPlayerNameplateVisualPatch
{
    [HarmonyPostfix]
    private static void ApplyRefreshedPortrait(PartyPlayerNameplateVM __instance)
    {
        if (ModInformation.IsClient && __instance._mainHeroVisualBind != null)
        {
            __instance.MainHeroVisual = __instance._mainHeroVisualBind;
        }
    }
}
