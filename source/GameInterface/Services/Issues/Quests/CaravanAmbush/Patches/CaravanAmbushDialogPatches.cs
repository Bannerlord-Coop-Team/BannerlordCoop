using Common;
using HarmonyLib;
using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushDialogPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.SetDialogs))]
    [HarmonyPrefix]
    private static bool Dialogs() => ModInformation.IsClient;

    // Vanilla dialog with a null-caravan guard and a one-shot end hook the Publicizer cannot bind directly.
    [HarmonyPatch(typeof(Quest), nameof(Quest.GetCaravaneerDialogFlow))]
    [HarmonyPrefix]
    private static bool CaravanDialog(Quest __instance, ref DialogFlow __result)
    {
        __result = DialogFlow.CreateDialogFlow("start", 125)
            .NpcLine("{=LqEdr7sQ}Thank you, {?PLAYER.GENDER}milady{?}sir{\\?}. {QUEST_GIVER.LINK} had informed me that help would be on the way. We needed it, I think. Those were a pretty tough lot.")
            .Condition(() =>
            {
                StringHelpers.SetCharacterProperties("QUEST_GIVER", __instance.QuestGiver.CharacterObject);
                return __instance._caravanParty != null && __instance._isCaravanSaved &&
                    CharacterObject.OneToOneConversationCharacter == ConversationHelper.GetConversationCharacterPartyLeader(__instance._caravanParty.Party);
            })
            .PlayerLine("{=MKbLhn9d}I'm glad we caught up to you in time.")
            .NpcLine("{=yxg91L0a}We'll tell everyone what you did.[if:convo_happy][ib:normal2] Please take some of these goods in compensation. We have no intention to sell them anyway. Safe travels, {?PLAYER.GENDER}milady{?}sir{\\?}.")
            .Consequence(() => CompleteOnConversationEnd(__instance))
            .CloseDialog();
        return false;
    }

    private static void CompleteOnConversationEnd(Quest quest)
    {
        var conversationManager = Campaign.Current.ConversationManager;
        // Publicizer exposes duplicate event metadata, so direct event binding does not compile here.
        var conversationEnd = AccessTools.Event(conversationManager.GetType(), "ConversationEndOneShot");
        if (conversationEnd == null)
            throw new MissingMemberException("ConversationEndOneShot");
        conversationEnd.AddEventHandler(conversationManager, new Action(quest.OnQuestSucceeded));
    }
}
