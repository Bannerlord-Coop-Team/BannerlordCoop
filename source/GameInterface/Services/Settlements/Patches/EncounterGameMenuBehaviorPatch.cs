using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior))]
    public class EncounterGameMenuBehaviorPatch
    {
        //These patches disables opening of these menus if EncounterSettlement is null as it seems to get called multiple times.
        [HarmonyPrefix]
        [HarmonyPatch("game_menu_town_outside_on_init")]
        public static bool Prefix(MenuCallbackArgs args)
        {
            if (PlayerEncounter.EncounterSettlement != null) return true;

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("game_menu_town_town_besiege_on_condition")]
        public static bool CheckFortificationEncounterSettlement(MenuCallbackArgs args)
        {
            if (PlayerEncounter.EncounterSettlement != null) return true;

            return false;
        }
        [HarmonyPatch("game_menu_army_talk_to_leader_on_consequence")]
        [HarmonyPrefix]
        private static bool ArmyTalkToLeaderConsequencePrefix()
        {
            var encounteredParty = PlayerEncounter.EncounteredParty;
            if (encounteredParty?.MobileParty?.IsPlayerParty() != true)
                return true; // Army leader is not player, let vanilla open the normal conversation

            // Route into the same player party interaction request pipeline used for regular P2P 

            MessageBroker.Instance.Publish(null, new ConversationRequested(
                encounteredParty,
                PartyBase.MainParty,
                forcePlayerOutFromSettlement: false,
                ConversationRestartSource.PlayerEncounter,
                true));
            return false;
        }
        [HarmonyPatch("game_menu_army_talk_to_other_members_item_on_consequence")]
        [HarmonyPrefix]
        private static bool ArmyTalkToOtherMembersItemConsequencePrefix(MenuCallbackArgs args)
        {
            var selectedParty = (args.MenuContext.GetSelectedObject() as MobileParty)?.Party;
            if (selectedParty?.MobileParty?.IsPlayerParty() != true)
                return true; // Army member is not player, let vanilla open the normal conversation

            //Route into the same player party interaction request pipeline used for regular P2P 

            MessageBroker.Instance.Publish(null, new ConversationRequested(
                selectedParty,
                PartyBase.MainParty,
                forcePlayerOutFromSettlement: false,
                ConversationRestartSource.PlayerEncounter,
                true));
            return false;
        }
    }
}
