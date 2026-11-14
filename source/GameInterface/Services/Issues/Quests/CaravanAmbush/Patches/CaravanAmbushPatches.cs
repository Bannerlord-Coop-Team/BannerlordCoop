using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushPatches
{
    [HarmonyPatch(typeof(CaravanAmbushIssueBehavior), nameof(CaravanAmbushIssueBehavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool Generate() => ModInformation.IsServer;

    [HarmonyPatch(typeof(CaravanAmbushIssueBehavior), nameof(CaravanAmbushIssueBehavior.OnGameLoadFinished))]
    [HarmonyPrefix]
    private static bool LoadBehavior() => ModInformation.IsServer;

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
    [HarmonyPrefix]
    private static bool Start(QuestBase __instance) => !(__instance is Quest) || ModInformation.IsClient;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnQuestAccepted))]
    [HarmonyPrefix]
    private static bool Accepted(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        __instance.StartQuest();
        if (__instance.JournalEntries.Count == 0) __instance.AddLog(__instance.CaravanAmbushIssueQuestActivatedLogText);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.HandlePartyAiAfterCompletion))]
    [HarmonyPrefix]
    // Party cleanup is centralized in ICaravanAmbushWorldEventService.End.
    private static bool Release() => false;

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
    [HarmonyPrefix]
    private static void Finalize(QuestBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Quest quest)) return;
        if (ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.End(quest);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalized(IssueBase __instance)
    {
        // The framework dismisses a disconnected owner's issue without finalizing its quest.
        if (ModInformation.IsClient || !(__instance.IssueQuest is Quest quest) || !quest.IsOngoing) return;
        if (ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.End(quest);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.InitializeQuestOnGameLoad))]
    [HarmonyPrefix]
    private static bool LoadQuest(Quest __instance)
    {
        if (ModInformation.IsClient)
        {
            __instance.SetDialogs();
            return false;
        }

        return true;
    }
}
