using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.PlayerCaptivityService.Patches.Disable;

[HarmonyPatch(typeof(PlayerCaptivityCampaignBehavior))]
internal class DisablePlayerCaptivityCampaignBehavior
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PlayerCaptivityCampaignBehavior), nameof(PlayerCaptivityCampaignBehavior.OnPrisonerTaken));
    }

    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
