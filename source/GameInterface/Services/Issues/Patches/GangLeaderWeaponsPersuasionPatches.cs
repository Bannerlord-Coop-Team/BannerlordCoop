using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[HarmonyPatch]
internal static class GangLeaderWeaponsPersuasionPatches
{
    [HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.DoOption), typeof(int))]
    [HarmonyPrefix]
    private static bool ChoosePrefix(ConversationManager __instance, int optionIndex)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer ||
            optionIndex < 0 || optionIndex >= __instance.CurOptions.Count) return true;
        var option = __instance.CurOptions[optionIndex];
        if (!option.HasPersuasion || __instance._sentences[option.SentenceNo].RelatedObject is not Quest quest ||
            GangLeaderWeaponsActionScope.Contains(quest)) return true;
        MessageBroker.Instance.Publish(quest, new GangLeaderWeaponsPersuasionOptionRequested(quest, __instance, optionIndex));
        return false;
    }

    [HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.PersuasionCommitProgress))]
    [HarmonyPrefix]
    private static bool CommitPrefix(PersuasionOptionArgs persuasionOptionArgs)
    {
        if (!GangLeaderWeaponsActionScope.IsLocalOwner ||
            GangLeaderWeaponsActionScope.Current?._task.Options.Contains(persuasionOptionArgs) != true) return true;
        CampaignEventDispatcher.Instance.OnPersuasionProgressCommitted(ConversationManager.GetPersuasionChosenOptions().Last());
        return false;
    }

    [HarmonyPatch(typeof(Quest), "guard_persuation_weapon_smuggling_selected_option_response_on_consequence")]
    [HarmonyPrefix]
    private static bool EffectsPrefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}
