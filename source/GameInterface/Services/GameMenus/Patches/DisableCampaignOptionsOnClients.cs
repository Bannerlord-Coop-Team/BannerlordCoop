using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Options.ManagedOptions;

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

[HarmonyPatch(typeof(ManagedOptionData))]
internal class DisableManagingOtherOptionsOnClients
{
    [HarmonyPatch(nameof(ManagedOptionData.GetIsDisabledAndReasonID))]
    [HarmonyPrefix]
    public static bool GetIsDisabledAndReasonWithIDPrefix(ManagedOptionData __instance, ref ValueTuple<string, bool> __result)
    {
        var type = __instance.Type;
        if (ModInformation.IsClient && type == ManagedOptions.ManagedOptionsType.PlayerReceivedDamageDifficulty)
        {
            __result = new("Managing this option is disabled on clients; the host does this.", true);
            return false;
        }

        return true;
    }
}