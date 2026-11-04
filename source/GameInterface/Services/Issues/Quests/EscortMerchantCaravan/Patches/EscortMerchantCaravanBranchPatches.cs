using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class EscortMerchantCaravanBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.conversation_caravan_fight_on_consequence))]
    [HarmonyPrefix]
    private static bool Attack(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return true;
        if (dispatcher.BeforeQuestBranch(__instance, (byte)EscortBranch.AttackCaravan)) return true;
        MapEventHelper.OnConversationEnd();
        return false;
    }
}
