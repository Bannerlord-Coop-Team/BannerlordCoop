using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

[HarmonyPatch(typeof(Quest), "CreateDeserterParty")]
internal class ExtortionQuestCreationPatches
{
    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance)
    {
        if (!ExtortionQuestMirrorScope.IsActive && ModInformation.IsServer) return true;
        ExtortionQuestMirrorScope.Apply(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.CreateDefenderParty))]
internal class ExtortionQuestDefenderCreationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        ExtortionQuestMirrorScope.ApplyDefender(__instance);
        return false;
    }
}
