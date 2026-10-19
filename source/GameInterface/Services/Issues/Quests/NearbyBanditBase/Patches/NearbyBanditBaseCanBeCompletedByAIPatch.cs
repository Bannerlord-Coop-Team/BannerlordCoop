using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System.Linq;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch(typeof(Issue), nameof(Issue.CanBeCompletedByAI))]
internal class NearbyBanditBaseCanBeCompletedByAIPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Issue __instance, ref bool __result)
    {
        if (ModInformation.IsClient)
        {
            return true;
        }

        __result = !__instance.TargetHideout.Party.PrisonRoster.GetTroopRoster()
            .Any(element => element.Character.HeroObject != null && element.Character.HeroObject.IsPlayerHero());

        return false;
    }
}
