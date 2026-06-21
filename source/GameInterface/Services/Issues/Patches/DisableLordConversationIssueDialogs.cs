using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

// Issues/quests are disabled in coop, but LordConversationsCampaignBehavior still adds the
// player options that route into them. Their handler dialogue lives in the disabled
// IssuesCampaignBehavior, so picking one dead-ends the conversation and soft-locks the game.
// Force the gating conditions to false so the options never appear. Remove when quests work.

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_hero_main_options_have_issue_on_condition")]
internal class DisableHeroGiveIssueOption
{
    static bool Prefix(ref bool __result) { __result = false; return false; }
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_task_given_on_condition")]
internal class DisableHeroTaskGivenOption
{
    static bool Prefix(ref bool __result) { __result = false; return false; }
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_task_given_alternative_on_condition")]
internal class DisableHeroTaskGivenAlternativeOption
{
    static bool Prefix(ref bool __result) { __result = false; return false; }
}
