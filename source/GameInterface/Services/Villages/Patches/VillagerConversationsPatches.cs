using Common.Messaging;
using Common.Util;
using GameInterface.Services.ItemRosters.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class VillagerConversationsPatches
{
    private static readonly bool villagerHostileActionsEnabled = true;

    [HarmonyPatch(nameof(VillagerCampaignBehavior.village_farmer_loot_on_clickable_condition))]
    [HarmonyPrefix]
    public static bool VillageFarmerLootOnClickableConditionPrefix(ref VillagerCampaignBehavior __instance, ref bool __result, out TextObject explanation)
    {
        if (!villagerHostileActionsEnabled)
        {
            __result = false;
            explanation = new TextObject("{=!} Hostile actions against villagers are temporarily disabled.");
            return false;
        }

        // Replacement message for vanilla's "You just looted these people." message to be ambiguous for more than one player
        explanation = new TextObject("");
        if (__instance._lootedVillagers.ContainsKey(MobileParty.ConversationParty))
        {
            explanation = new TextObject("{=!}These villagers have been looted recently.", null);
            __result = false;
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.village_farmer_talk_start_on_condition))]
    [HarmonyPostfix]
    public static void VillageFarmerStartTalkOnConditionPostfix(ref VillagerCampaignBehavior __instance)
    {
        var encounteredParty = PlayerEncounter.EncounteredParty;
        if (PlayerEncounter.Current == null
            || Campaign.Current.CurrentConversationContext != ConversationContext.PartyEncounter
            || !encounteredParty.IsMobile
            || !encounteredParty.MobileParty.IsVillager)
        {
            return;
        }

        // Local update is needed so separately publish message to server in postfix to store change in CoopSession
        var message = new SetPlayerVillagersInteraction(Hero.MainHero, MobileParty.ConversationParty, __instance.GetPlayerInteraction(encounteredParty.MobileParty));
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.conversation_player_decided_to_buy_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationPlayerDecidedToBuyOnConsequencePrefix(ref VillagerCampaignBehavior __instance)
    {
        if (MobileParty.ConversationParty.IsVillager && MobileParty.ConversationParty.ItemRoster.Count > 0)
        {
            var message = new PlayerBoughtFromVillagersOnConsequence(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty);
            MessageBroker.Instance.Publish(__instance, message);
        }
        PlayerEncounter.LeaveEncounter = true;

        return false;
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.conversation_village_farmer_took_prisoner_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationVillageFarmerTookPrisonerOnConsequencePrefix(ref VillagerCampaignBehavior __instance)
    {
        MobileParty encounteredMobileParty = PlayerEncounter.EncounteredMobileParty;

        // Call helper function to implement vanilla open loot screen logic
        ContainerProvider.TryResolve<IItemRosterInterface>(out var itemRosterInterface);
        itemRosterInterface.OpenPartyLootScreen(encounteredMobileParty, out var _, out var itemRosterElements);

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
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);

        PlayerEncounter.LeaveEncounter = true;

        var message = new VillagersTookPrisonerOnConsequence(Hero.MainHero, MobileParty.MainParty, encounteredMobileParty, itemRosterElements);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.conversation_village_farmer_fight_on_consequence))]
    [HarmonyPostfix]
    public static void ConversationVillageFarmerFightOnConsequencePostfix(ref VillagerCampaignBehavior __instance)
    {
        // Local update is needed so separately publish message to server in postfix to store change in CoopSession
        var message = new SetPlayerVillagersInteraction(Hero.MainHero, MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.conversation_village_farmer_fight_forced_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationVillageFarmerFightForcedOnConsequencePrefix(ref VillagerCampaignBehavior __instance)
    {
        // Update last interaction with villagers locally
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);

        // Send message to server to update CoopSession and run BeHostileAction.ApplyEncounterHostileAction
        var message = new ApplyHostileVillagersInteraction(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.conversation_village_farmer_looted_leave_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationVillageFarmerLootedLeaveOnConsequencePrefix(ref VillagerCampaignBehavior __instance)
    {
        // Locally calculate bribe amount with allowed thread added by AllowItemRostersInGUI to handle itemRoster
        __instance.CalculateConversationPartyBribeAmount(out int amount, out ItemRoster itemRoster);

        // Locally set player interaction, and then save in CoopSession on server
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);

        PlayerEncounter.LeaveEncounter = true;

        var message = new VillagersLootedLeaveOnConsequence(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty, itemRoster._data, amount);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(VillagerCampaignBehavior.conversation_village_farmer_surrender_leave_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationVillageFarmerSurrenderOnLeaveConsequencePrefix(ref VillagerCampaignBehavior __instance)
    {
        // Call helper function to implement vanilla open loot screen logic
        ContainerProvider.TryResolve<IItemRosterInterface>(out var itemRosterInterface);
        itemRosterInterface.OpenPartyLootScreen(MobileParty.ConversationParty, out var _, out var itemRosterElements);

        // Locally set player interaction, and then save in CoopSession on server
        __instance.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);

        PlayerEncounter.LeaveEncounter = true;

        var message = new VillagersSurrenderLeaveOnConsequence(Hero.MainHero, MobileParty.MainParty, MobileParty.ConversationParty, itemRosterElements);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    /*
    private static void OpenLootScreen(MobileParty encounterParty, out ItemRosterElement[] itemRosterElements)
    {
        ItemRoster itemRoster = null;
        using (new AllowedThread())
        {
            itemRoster = new ItemRoster(encounterParty.ItemRoster);

            itemRosterElements = itemRoster._data;

            if (itemRoster.Count > 0)
            {
                InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster>
                {
                    { PartyBase.MainParty, itemRoster }
                });
            }
        }
    }
    */
}
