using Common.Messaging;
using Common.Util;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ItemRosters.Interfaces;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravansCampaignBehavior))]
internal class CaravansConversationsPatches
{
    private static readonly bool caravanHostileActionsEnabled = true;

    [HarmonyPatch(nameof(CaravansCampaignBehavior.caravan_companion_ask_change_home_settlement_4_on_consequence))]
    [HarmonyPrefix]
    public static bool CaravanCompanionAskChangeHomeSettlement4OnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        Settlement settlement = ConversationSentence.SelectedRepeatObject as Settlement;
        StringHelpers.SetSettlementProperties("SETTLEMENT", settlement, null, false);

        // Update locally to update dialogue properly before being managed by the server to update all other clients
        using (new AllowedThread())
        {
            MobileParty.ConversationParty.CaravanPartyComponent.ChangeHomeSettlement(settlement);
        }

        var message = new ChangeCaravanHomeSettlement(MobileParty.ConversationParty, settlement);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.caravan_companion_prohibit_kingdoms_selected_2_on_consequence))]
    [HarmonyPrefix]
    public static bool CaravanCompanionProhibitKingdomsSelected2OnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        Kingdom kingdom = ConversationSentence.SelectedRepeatObject as Kingdom;
        bool kingdomAlreadyProhibited = __instance._prohibitedKingdomsForPlayerCaravans.Contains(kingdom);

        // Send message to server to update CoopSession
        var message = new ToggleProhibitedKingdom(Hero.MainHero, kingdom, kingdomAlreadyProhibited);
        MessageBroker.Instance.Publish(__instance, message);

        // Modifying local instance of _prohibitedKingdomsForPlayerCaravans on client is needed
        return true;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.conversation_caravan_fight_forced_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationCaravanFightForcedOnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        // Update last interaction with caravan locally
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, CaravansCampaignBehavior.PlayerInteraction.Hostile);

        // Send message to server to update CoopSession and run BeHostileAction.ApplyEncounterHostileAction
        var message = new ApplyHostileCaravanInteraction(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.caravan_start_talk_on_condition))]
    [HarmonyPostfix]
    public static void CaravanStartTalkOnConditionPostfix(ref CaravansCampaignBehavior __instance)
    {
        if (MobileParty.ConversationParty == null || !MobileParty.ConversationParty.IsCaravan)
        {
            return;
        }

        // Local update is needed so separately publish message to server in postfix to store change in CoopSession
        var message = new SetPlayerCaravanInteraction(Hero.MainHero, MobileParty.ConversationParty, CaravansCampaignBehavior.PlayerInteraction.Friendly);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.caravan_loot_on_clickable_condition))]
    [HarmonyPrefix]
    public static bool CaravanLootOnClickableConditionPrefix(ref CaravansCampaignBehavior __instance, ref bool __result, out TextObject explanation)
    {
        if (!caravanHostileActionsEnabled)
        {
            __result = false;
            explanation = new TextObject("{=!} Hostile actions against caravans are temporarily disabled.");
            return false;
        }

        // Replacement message for vanilla's "You just looted this party." message to be ambiguous for more than one player
        explanation = new TextObject("");
        if (__instance._lootedCaravans.ContainsKey(MobileParty.ConversationParty))
        {
            explanation = new TextObject("{=!}This caravan has been looted recently.", null);
            __result = false;
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.caravan_ask_trade_rumors_on_consequence))]
    [HarmonyPostfix]
    public static void CaravanAskTradeRumorsOnConsequencePostfix(ref CaravansCampaignBehavior __instance)
    {
        // Send data to server to update CoopSession
        var message = new UpdateTradeRumorTakenCaravans(Hero.MainHero, __instance._tradeRumorTakenCaravans);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.conversation_caravan_fight_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationCaravanFightOnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        // Update last interaction with caravan locally
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, CaravansCampaignBehavior.PlayerInteraction.Hostile);

        // Send message to server to update CoopSession and run BeHostileAction.ApplyEncounterHostileAction
        var message = new ApplyHostileCaravanInteraction(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.conversation_caravan_looted_leave_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationCaravanLootedLeaveOnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        // Locally calculate bribe amount with allowed thread added by AllowItemRostersInGUI to handle itemRoster
        __instance.BribeAmount(MobileParty.ConversationParty.Party, out int amount, out ItemRoster itemRoster);

        // Locally set player interaction, and then save in CoopSession on server
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, CaravansCampaignBehavior.PlayerInteraction.Hostile);

        PlayerEncounter.LeaveEncounter = true;

        var message = new CaravanLootedLeaveOnConsequence(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty, itemRoster._data, amount);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.conversation_caravan_surrender_leave_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationCaravanSurrenderLeaveOnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        // Call helper function to implement vanilla open loot screen logic
        ContainerProvider.TryResolve<IItemRosterInterface>(out var itemRosterInterface);
        itemRosterInterface.OpenPartyLootScreen(MobileParty.ConversationParty, out var caravanHasItems, out var itemRosterElements);

        // Locally set player interaction, and then save in CoopSession on server
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, CaravansCampaignBehavior.PlayerInteraction.Hostile);

        PlayerEncounter.LeaveEncounter = true;

        var message = new CaravanSurrenderLeaveOnConsequence(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty, caravanHasItems, itemRosterElements);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CaravansCampaignBehavior.conversation_caravan_took_prisoner_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationCaravanTookPrisonerOnConsequencePrefix(ref CaravansCampaignBehavior __instance)
    {
        MobileParty encounteredMobileParty = PlayerEncounter.EncounteredMobileParty;

        // Call helper function to implement vanilla open loot screen logic
        ContainerProvider.TryResolve<IItemRosterInterface>(out var itemRosterInterface);
        itemRosterInterface.OpenPartyLootScreen(encounteredMobileParty, out var caravanHasItems, out var itemRosterElements);

        // Open prisoner transfer screen
        using (new AllowedThread())
        {
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (TroopRosterElement troopRosterElement in encounteredMobileParty.MemberRoster.GetTroopRoster())
            {
                troopRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
            }
            PartyScreenHelper.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, encounteredMobileParty.Name, troopRoster.TotalManCount, null);
        }

        // Locally set player interaction, and then save in CoopSession on server
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, CaravansCampaignBehavior.PlayerInteraction.Hostile);

        PlayerEncounter.LeaveEncounter = true;

        var message = new CaravanTookPrisonerOnConsequence(Hero.MainHero, MobileParty.MainParty, encounteredMobileParty, caravanHasItems, itemRosterElements);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    /*
    private static void OpenLootScreen(MobileParty encounterParty, out bool caravanHasItems, out ItemRosterElement[] itemRosterElements)
    {
        ItemRoster itemRoster = null;
        using (new AllowedThread())
        {
            itemRoster = new ItemRoster(encounterParty.ItemRoster);
        }

        itemRosterElements = itemRoster._data;

        caravanHasItems = false;
        for (int i = 0; i < itemRoster.Count; i++)
        {
            if (itemRoster.GetElementNumber(i) > 0)
            {
                caravanHasItems = true;
                break;
            }
        }
        if (caravanHasItems)
        {
            InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster>
            {
                {
                    PartyBase.MainParty,
                    itemRoster
                }
            });
        }
    }
    */
}
