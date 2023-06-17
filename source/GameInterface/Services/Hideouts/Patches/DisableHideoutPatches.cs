using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Patches
{
    /// <summary>
    /// Disables hideout functionality. Will be removed when
    /// Hideouts are ready to be implemented
    /// </summary>
    [HarmonyPatch(typeof(Hideout))]
    internal class DisableHideoutPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Hideout.IsSpotted), MethodType.Setter)]
        private static bool DisableIsSpotted(ref Hideout __instance)
        {
            if (__instance?.Owner?.Settlement != null)
            {
                __instance.Owner.Settlement.IsVisible = false;
            }
            
            return false;
        }
    }
}
