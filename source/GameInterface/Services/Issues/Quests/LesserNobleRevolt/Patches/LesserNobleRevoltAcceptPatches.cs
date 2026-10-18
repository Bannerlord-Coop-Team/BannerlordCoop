using Common;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltAcceptPatches
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddDialogs))]
    [HarmonyPrefix]
    private static bool AddDialogs(QuestBase __instance) => __instance is not Quest || ModInformation.IsClient;

    [HarmonyPatch(typeof(Quest), MethodType.Constructor,
        new[] { typeof(string), typeof(Hero), typeof(CampaignTime), typeof(int), typeof(TextObject) })]
    [HarmonyPrefix]
    private static void UseClientPartyPlaceholder(Quest __instance)
    {
        if (ModInformation.IsClient)
        {
            // Vanilla dereferences this field after spawning. The real server party is attached by the handoff message.
            __instance._lesserNobleParty = MobileParty.MainParty;
        }
    }
    [HarmonyPatch(typeof(Quest), nameof(Quest.SpawnLesserNobleParty))]
    [HarmonyPrefix]
    private static bool SpawnParty() => ModInformation.IsServer;

    // Runs before TakePrisonerActionPatches, whose later prefix is then skipped, so a protected owner is
    // neither captured nor parked through its PrisonerTaken postfix.
    [HarmonyPatch(typeof(TakePrisonerAction), nameof(TakePrisonerAction.ApplyInternal))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool ProtectOwnerFromCapture(Hero prisonerCharacter)
    {
        return CallOriginalPolicy.IsOriginalAllowed() ||
            ModInformation.IsClient ||
            !LesserNobleRevoltWorldEventPatches.IsProtectedHero(prisonerCharacter);
    }
}
