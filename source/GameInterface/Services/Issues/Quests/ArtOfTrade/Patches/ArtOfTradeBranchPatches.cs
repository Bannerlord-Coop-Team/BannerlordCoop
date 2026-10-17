using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade.Patches;

[HarmonyPatch]
internal class ArtOfTradeBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool Commission(Quest __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (__instance._soldItemAmountLog != null || !__instance.IsOngoing) return false;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return false;

        // The client starts its real quest and journal; the server grants the goods once.
        __instance.StartQuest();
        __instance.AddTrackedObject(__instance.QuestGiver.CurrentSettlement);
        __instance.AddLog(__instance.QuestStartedLogText1);
        __instance.AddLog(__instance.QuestStartedLogText2);
        __instance.AddLog(__instance.QuestStartedLogText3);
        var sold = new TextObject("{=GaXEmoFy}Sold {.%}{SELECTED_ITEM}{.%} amount:");
        sold.SetTextVariable("SELECTED_ITEM", __instance._selectedItemObject.Name);
        var denars = new TextObject("{=pwnqk0Nj}Gathered denars from {.%}{SELECTED_ITEM}{.%}");
        denars.SetTextVariable("SELECTED_ITEM", __instance._selectedItemObject.Name);
        __instance._soldItemAmountLog = __instance.AddDiscreteLog(TextObject.GetEmpty(), sold, 0, __instance._selectedItemObjectCount);
        __instance._gatheredDenarsLog = __instance.AddDiscreteLog(TextObject.GetEmpty(), denars, 0, __instance._targetDenarsToAchieve);
        dispatcher.BeforeQuestBranch(__instance, (byte)ArtOfTradeBranch.SellOnCommission);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestFinishedPlayerBoughtTheGoods))]
    [HarmonyPrefix]
    private static bool Buy(Quest __instance) => BeforeBranch(__instance, ArtOfTradeBranch.BuyGoods);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestSuccessPlayerSoldTheProducts))]
    [HarmonyPrefix]
    private static bool Proceeds(Quest __instance) => BeforeBranch(__instance, ArtOfTradeBranch.PaySaleProceeds);

    [HarmonyPatch(typeof(Quest), nameof(Quest.PlayerPaidAgreedDenarsQuestSuccess))]
    [HarmonyPrefix]
    private static bool Pay(Quest __instance) => BeforeBranch(__instance, ArtOfTradeBranch.PayAgreedSum);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestFailedPlayerBrokeTheAgreement))]
    [HarmonyPrefix]
    private static bool Break(Quest __instance) => BeforeBranch(__instance, ArtOfTradeBranch.BreakAgreement);

    private static bool BeforeBranch(Quest quest, ArtOfTradeBranch branch)
    {
        if (ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowed())
            MessageBroker.Instance.Publish(quest, new ArtOfTradeProgressChanged(quest));
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return false;
        if (dispatcher.BeforeQuestBranch(quest, (byte)branch)) return true;
        MapEventHelper.OnConversationEnd();
        return false;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
    [HarmonyPrefix]
    private static bool Start(QuestBase __instance) => !(__instance is Quest) || ModInformation.IsClient;

    [HarmonyPatch(typeof(Quest), nameof(Quest.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEvents() => ModInformation.IsClient;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnPlayerInventoryExchange))]
    [HarmonyPrefix]
    private static bool Sale(Quest __instance) => ModInformation.IsClient && __instance._soldItemAmountLog != null;

    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTick))]
    [HarmonyPrefix]
    private static bool HourlyTick() => ModInformation.IsClient;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnPlayerInventoryExchange))]
    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTick))]
    [HarmonyPostfix]
    private static void Progress(Quest __instance)
    {
        if (ModInformation.IsClient && __instance.IsOngoing && __instance._soldItemAmountLog != null)
            MessageBroker.Instance.Publish(__instance, new ArtOfTradeProgressChanged(__instance));
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
    [HarmonyPrefix]
    private static void Finalize(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest quest)
            MessageBroker.Instance.Publish(quest, new ArtOfTradeJournalFinalizing(quest));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool Timeout() => ModInformation.IsServer;
}
