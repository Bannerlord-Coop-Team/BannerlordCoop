using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace GameInterface.Services.GameMenus.Patches;

/// <summary>
/// Disable changing campaign options on clients.
/// This solution still lets clients see the current configuration while preventing any changes.
/// </summary>
[HarmonyPatch(typeof(CampaignOptionData))]
internal class DisableManagingCampaignOptionsOnClients
{
    [HarmonyPatch(nameof(CampaignOptionData.GetIsDisabledWithReason))]
    [HarmonyPrefix]
    public static bool GetIsDisabledWithReasonPrefix(CampaignOptionData __instance, ref CampaignOptionDisableStatus __result)
    {
        if (ModInformation.IsClient)
        {
            __result = new(true, "Managing campaign options is disabled on clients; the host does this.", -1f);
            return false;
        }

        return true;
    }
}