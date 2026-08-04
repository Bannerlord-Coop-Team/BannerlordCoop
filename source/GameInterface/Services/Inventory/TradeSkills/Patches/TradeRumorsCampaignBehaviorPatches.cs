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
    public static bool OnSettlementEnteredPrefix()
    {
        return !ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(TradeRumorsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPostfix]
    public static void OnSettlementEnteredPostfix(TradeRumorsCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
    {
        if (ModInformation.IsServer)
            return;

        if (mobileParty == null
            || (!mobileParty.IsMainParty
            && (!mobileParty.IsCaravan
            || mobileParty.Party.Owner == null
            || mobileParty.Party.Owner.Clan != Clan.PlayerClan
            || !Hero.MainHero.GetPerkValue(DefaultPerks.Trade.TravelingRumors)))
            || !settlement.IsTown) return;

        var message = new UpdateTradeRumors(__instance._tradeRumors, __instance._enteredSettlements);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
