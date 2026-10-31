using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
internal class NearbyBanditBaseTimedOutPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        __instance.AddLog(__instance.OnQuestFailedLogText);

        return false;
    }
}
