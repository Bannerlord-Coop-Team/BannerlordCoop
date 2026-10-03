using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[HarmonyPatch(typeof(Quest))]
internal sealed class ArtisanProductQuestActionPatches
{
    [HarmonyPatch("DeliverItemsPartiallyOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverPartially(Quest __instance) => Request(__instance, ArtisanProductQuestAction.DeliverPartially);

    [HarmonyPatch("DeliverItemsFullyOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverFully(Quest __instance) => Request(__instance, ArtisanProductQuestAction.DeliverFully);

    [HarmonyPatch("DeliverItemsRejectOnConsequence")]
    [HarmonyPrefix]
    private static bool RefuseDelivery(Quest __instance) => Request(__instance, ArtisanProductQuestAction.RefuseDelivery);

    [HarmonyPatch("QuestFailedWithRefusal")]
    [HarmonyPrefix]
    private static bool AcceptMerchantOffer(Quest __instance) => Request(__instance, ArtisanProductQuestAction.AcceptMerchantOffer);

    [HarmonyPatch("RefuseCounterOfferConsequences")]
    [HarmonyPrefix]
    private static bool RefuseMerchantOffer(Quest __instance) => Request(__instance, ArtisanProductQuestAction.RefuseMerchantOffer);

    private static bool Request(Quest quest, ArtisanProductQuestAction action)
    {
        if (ModInformation.IsServer) return true;
        MessageBroker.Instance.Publish(quest, new ArtisanProductQuestActionRequested(
            quest.QuestGiver, action, quest._deliveredRawGoods));
        return false;
    }
}
