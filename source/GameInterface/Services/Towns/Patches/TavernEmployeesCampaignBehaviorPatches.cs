using Common;
using Common.Messaging;
using GameInterface.Services.Towns.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(TavernEmployeesCampaignBehavior))]
internal class TavernEmployeesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix(TavernEmployeesCampaignBehavior __instance)
    {
        // Only let server update on ticks
        if (ModInformation.IsClient) return false;

        // Update on server to persist in CoopSession and send updated values to clients
        var message = new DailyTickDrinkThisDayInSettlement();
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.WeeklyTick))]
    [HarmonyPrefix]
    public static bool WeeklyTickPrefix(TavernEmployeesCampaignBehavior __instance)
    {
        // Only let server update on ticks
        if (ModInformation.IsClient) return false;

        // Update on server to persist in CoopSession and send updated values to clients
        var message = new WeeklyTickHasBoughtTunToParty();
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.player_accepts_clan_info_offer_on_consequence))]
    [HarmonyPrefix]
    public static bool PlayerAcceptsClanInfoOfferOnConsequencePrefix(TavernEmployeesCampaignBehavior __instance)
    {
        // Doesn't do anything. Every hero is already known to every player at game start
        foreach (Hero hero in Settlement.CurrentSettlement.OwnerClan.Heroes)
        {
            hero.IsKnownToPlayer = true;
        }

        // Run gold change on server
        var message = new PlayerAcceptsClanInfoOffer(Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.conversation_tavernmaid_delivers_food_on_consequence))]
    [HarmonyPostfix]
    public static void ConversationTavernmaidDeliversFoodOnConsequencePostfix(TavernEmployeesCampaignBehavior __instance)
    {
        // Update on server to persist in CoopSession
        var message = new TavernMaidDeliversFood(Hero.MainHero, Settlement.CurrentSettlement);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.can_buy_tun_on_consequence))]
    [HarmonyPrefix]
    public static bool CanBuyTunOnConsequencePrefix(TavernEmployeesCampaignBehavior __instance)
    {
        int tunPrice = TavernEmployeesCampaignBehavior.get_tun_price();
        __instance._hasBoughtTunToParty = true;

        // Run gold & morale change on server
        var message = new PlayerBuysTun(Hero.MainHero, tunPrice);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.conversation_ransom_broker_start_on_condition))]
    [HarmonyPrefix]
    public static void ConversationRansomBrokerStartOnConditionPrefix(TavernEmployeesCampaignBehavior __instance)
    {
        if (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.RansomBroker && !__instance._hasMetWithRansomBroker)
        {
            // Update on server to persist in CoopSession
            var message = new UpdateHasMetRansomBroker(Hero.MainHero, true);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }

    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.FindCompanionWithType))]
    [HarmonyPatch(new Type[] { typeof(TavernEmployeesCampaignBehavior.TavernInquiryCompanionType) })]
    [HarmonyPostfix]
    public static void FindCompanionWithTypePostfix(TavernEmployeesCampaignBehavior __instance)
    {
        var message = new TavernKeeperFindCompanion(Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
