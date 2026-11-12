using Common;
using Common.Messaging;
using GameInterface.Policies;
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

        MessageBroker.Instance.Publish(__instance, new HideoutCampaignConsequenceRequested(
            __instance.Settlement, HideoutCampaignConsequence.SetAttackCooldown));
        return false;
    }
}
