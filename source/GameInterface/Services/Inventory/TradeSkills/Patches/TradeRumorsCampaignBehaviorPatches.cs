using Common;
using Common.Messaging;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Inventory.TradeSkills.Patches;

[HarmonyPatch(typeof(TradeRumorsCampaignBehavior))]
internal class TradeRumorsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(TradeRumorsCampaignBehavior.OnTradeRumorIsTaken))]
    [HarmonyPrefix]
    public static bool OnTradeRumorIsTakenPrefix(TradeRumorsCampaignBehavior __instance, List<TradeRumor> newRumors, Settlement sourceSettlement = null)
    {
        if (ModInformation.IsServer)
            return false;

        __instance.AddTradeRumors(newRumors, sourceSettlement);

        var message = new UpdateTradeRumors(__instance._tradeRumors, __instance._enteredSettlements);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(TradeRumorsCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix(TradeRumorsCampaignBehavior __instance)
    {
        if (ModInformation.IsServer)
            return false;

        __instance.AddDailyTradeRumors(1);
        __instance.DeleteExpiredRumors();
        __instance.DeleteExpiredEnteredSettlements();

        var message = new UpdateTradeRumors(__instance._tradeRumors, __instance._enteredSettlements);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(TradeRumorsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(TradeRumorsCampaignBehavior __instance, Settlement settlement, out long __state)
    {
        __state = __instance._enteredSettlements.TryGetValue(settlement, out var entered) ? entered._numTicks : -1;
        return !ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(TradeRumorsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPostfix]
    public static void OnSettlementEnteredPostfix(TradeRumorsCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero, long __state)
    {
        if (ModInformation.IsServer)
            return;

        if (!__instance._enteredSettlements.TryGetValue(settlement, out var entered)
            || entered._numTicks == __state) return;

        var message = new UpdateTradeRumors(__instance._tradeRumors, __instance._enteredSettlements);
        MessageBroker.Instance.Publish(__instance, message);
    }
}