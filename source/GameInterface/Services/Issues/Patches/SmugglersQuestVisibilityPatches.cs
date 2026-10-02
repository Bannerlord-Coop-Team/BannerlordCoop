using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Handlers;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Patches;

using Quest = SmugglersIssueBehavior.SmugglersIssueQuest;
using Issue = SmugglersIssueBehavior.SmugglersIssue;

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal class SmugglersJournalVisibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestsVM __instance)
    {
        if (ModInformation.IsServer) return;
        if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return;
        var entries = owners.Snapshot();
        foreach (var item in __instance.ActiveQuestsList.ToArray())
        {
            if (item.Quest is Quest && (!owners.TryGet(item.Quest, out var player) || player != Hero.MainHero))
                __instance.ActiveQuestsList.Remove(item);
            else if (item.Issue is Issue && (!owners.TryGet(item.Issue, out var alternativePlayer) || alternativePlayer != Hero.MainHero))
                __instance.ActiveQuestsList.Remove(item);
        }

        foreach (var item in __instance.OldQuestsList.ToArray())
        {
            if (item.QuestLogEntry != null && entries.Any(entry => entry.Value != Hero.MainHero
                && item.QuestLogEntry.IsRelatedTo(entry.Key)))
                __instance.OldQuestsList.Remove(item);
        }

        if (!__instance.ActiveQuestsList.Contains(__instance.SelectedQuest)
            && !__instance.OldQuestsList.Contains(__instance.SelectedQuest))
            __instance.SetSelectedItem(__instance.ActiveQuestsList.FirstOrDefault() ?? __instance.OldQuestsList.FirstOrDefault());
        __instance.IsThereAnyQuest = __instance.ActiveQuestsList.Count + __instance.OldQuestsList.Count > 0;
        __instance.RefreshValues();
    }
}

[HarmonyPatch]
internal class SmugglersQuestTrackPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddTrackedObject))]
    private static bool AddObjectPrefix(QuestBase __instance) => CanTrack(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.AddTrackedObjectForQuest))]
    private static bool AddQuestPrefix(QuestBase relatedQuest) => CanTrack(relatedQuest);

    private static bool CanTrack(QuestBase relatedQuest)
    {
        if (relatedQuest is not Quest || ModInformation.IsServer) return true;
        return ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)
            && owners.TryGet(relatedQuest, out var player) && player == Hero.MainHero;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Campaign), nameof(Campaign.OnPlayerCharacterChanged))]
    internal static void RefreshLocalTracks()
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return;
        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>())
        {
            if (!owners.TryGet(quest, out var player) || player != Hero.MainHero)
            {
                Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(quest);
                continue;
            }
            foreach (var tracked in new ITrackableCampaignObject[] { quest.QuestGiver, quest._targetSettlement, quest._originSettlement })
                if (!quest.IsTracked(tracked)) quest.AddTrackedObject(tracked);
        }
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnGameLoaded))]
internal class SmugglersQuestLoadedTracksPatch
{
    [ThreadStatic]
    internal static bool IsLoading;

    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = IsLoading;
        IsLoading = true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => IsLoading = __state;

    [HarmonyPostfix]
    private static void Postfix(QuestManager __instance)
    {
        if (ContainerProvider.TryResolve<QuestTraitProgressHandler>(out var traits)) traits.RestoreLocalProgress();
        SmugglersQuestTrackPatch.RefreshLocalTracks();
    }
}

[HarmonyPatch(typeof(ConversationSentence), nameof(ConversationSentence.RunCondition))]
internal class SmugglersConversationOwnerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ConversationSentence __instance, ref bool __result)
    {
        if (__instance.RelatedObject is not Quest quest) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority)
            && authority.IsLocalOwner(quest)) return true;
        __result = false;
        return false;
    }
}
