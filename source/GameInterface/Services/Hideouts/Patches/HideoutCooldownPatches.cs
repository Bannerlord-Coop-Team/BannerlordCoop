using Common;
using GameInterface.Policies;
using GameInterface.Services.Hideouts.Patches.Disable;
using GameInterface.Services.Hideouts.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Patches;

/// <summary>Leaves the hideout attack cooldown to the server, which replicates it to every client.</summary>
[HarmonyPatch(typeof(Hideout))]
internal class HideoutCooldownPatches
{
    // Native send troops and failed hideout encounters set it on the client, always to HideoutHiddenDuration like the server request.
    [HarmonyPatch(nameof(Hideout.SetNextPossibleAttackTime))]
    [HarmonyPrefix]
    private static bool SetNextPossibleAttackTimePrefix(Hideout __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        // A raid mission's cooldown was already set by the server when it started, so a lost fight has nothing to send.
        if (__instance.NextPossibleAttackTime.IsPast)
            HideoutCampaignBehaviorPatch.PublishConsequence(HideoutCampaignConsequence.SetAttackCooldown, __instance.Settlement);
        return false;
    }
}
