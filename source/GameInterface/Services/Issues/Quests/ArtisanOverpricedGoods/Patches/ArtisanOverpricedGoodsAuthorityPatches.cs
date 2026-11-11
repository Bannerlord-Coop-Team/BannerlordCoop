using Common;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ArtisanOverpricedGoodsAuthorityPatches
{
    [HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
    [HarmonyPrefix]
    private static bool FundingPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Issue), nameof(Issue.LordSolutionCondition))]
    [HarmonyPrefix]
    private static bool LordConditionPrefix(Issue __instance, ref bool __result, ref TextObject explanation)
    {
        // Settlement.CurrentSettlement is a campaign-wide encounter on the server, not the owner's town.
        if (ModInformation.IsClient) return true;
        __result = MobileParty.MainParty.CurrentSettlement == __instance.IssueSettlement &&
            Clan.PlayerClan == __instance.IssueSettlement.OwnerClan;
        explanation = __result ? null : new TextObject("{=bItEf0WN}You need to be the {?PLAYER.GENDER}lady{?}lord{\\?} of this settlement.");
        return false;
    }

    [HarmonyPatch(typeof(Issue), nameof(Issue.DoTroopsSatisfyAlternativeSolution))]
    [HarmonyPrefix]
    private static bool AlternativeEligibilityPrefix(Issue __instance, TroopRoster troopRoster, ref bool __result, ref TextObject explanation)
    {
        if (ModInformation.IsClient) return true;
        var companion = troopRoster.GetTroopRoster().FirstOrDefault(element => element.Character.IsHero).Character?.HeroObject;
        if (Hero.MainHero.Gold >= __instance.RequiredGoldForAlternativeSolution && companion != null) return true;

        __result = false;
        explanation = new TextObject("{=!}The companion or purchase funding is unavailable.");
        return false;
    }
}
