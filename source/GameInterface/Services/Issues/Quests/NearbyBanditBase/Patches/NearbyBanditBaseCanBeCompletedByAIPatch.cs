using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System.Linq;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
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

        // vanilla checks the main hero, which the server doesnt have, so any player held in the hideout keeps AI lords away
        var playerHeldInHideout = __instance.TargetHideout.Party.PrisonerHeroes.Any(prisoner => prisoner.HeroObject.IsPlayerHero());
        __result = !playerHeldInHideout;

        return false;
    }
}
