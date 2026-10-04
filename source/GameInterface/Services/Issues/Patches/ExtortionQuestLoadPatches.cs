using Common;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal class ExtortionQuestJournalSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IExtortionQuestJournal>(out var journal)) journal.SyncData(dataStore);
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.InitializeQuestOnGameLoad))]
internal class ExtortionQuestLoadPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => false;
}

[HarmonyPatch(typeof(HeroInterface), nameof(HeroInterface.SwitchToPlayer))]
internal class ExtortionQuestPlayerLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return;
        if (GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(Hero.MainHero, out var progress))
            foreach (var trait in progress.GetProperties())
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(trait, progress.GetPropertyValue(trait));
        var manager = Campaign.Current.QuestManager;
        foreach (var quest in manager.Quests.OfType<Quest>().ToArray())
        {
            if (!ownership.IsLocalPeerOwner(quest.QuestGiver))
            {
                quest.ClearRelatedFields();
                quest.RemoveAllTrackedObjects();
                quest.RemoveAllMapMarkers();
                manager.OnQuestFinalized(quest);
                if (quest.QuestGiver.Issue?.IssueQuest == quest) quest.QuestGiver.Issue.IssueQuest = null;
                continue;
            }

            // The transferred save is loaded before SwitchToPlayer binds the local hero.
            if (!quest.IsOngoing || quest.OfferDialogFlow != null) continue;
            StringHelpers.SetCharacterProperties("PLAYER", CharacterObject.PlayerCharacter);
            quest.SetDialogs();
            quest.AddDialogs();
            Campaign.Current.ConversationManager.AddDialogFlow(quest.QuestCompletionDialogFlow, quest);
            Campaign.Current.ConversationManager.AddDialogFlow(quest.DeserterPartyAmbushedDialogFlow, quest);
        }
    }
}
