using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal class CoopClanDialoguePatches
{
    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior), nameof(CompanionRolesCampaignBehavior.companion_fire_condition))]
    [HarmonyPostfix]
    public static void CompanionFireConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior), nameof(CompanionRolesCampaignBehavior.lead_a_party_clickable_condition))]
    [HarmonyPostfix]
    public static void LeadAPartyClickableConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_clan_member_manage_inventory_on_condition))]
    [HarmonyPostfix]
    public static void ConversationClanMemberManageInventoryOnConditionPostfix(ref bool __result)
        => CheckCanManageParty(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_clan_member_manage_troops_on_condition))]
    [HarmonyPostfix]
    public static void ConversationClanMemberManageTroopsOnConditionPostfix(ref bool __result)
        => CheckCanManageParty(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_hero_hire_on_condition))]
    [HarmonyPostfix]
    public static void ConversationHeroHireOnConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(CaravanConversationsCampaignBehavior), nameof(CaravanConversationsCampaignBehavior.conversation_caravan_build_on_condition))]
    [HarmonyPostfix]
    public static void ConversationCaravanBuildOnConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(WorkshopsCharactersCampaignBehavior), nameof(WorkshopsCharactersCampaignBehavior.can_player_buy_workshop_clickable_condition))]
    [HarmonyPostfix]
    public static void CanPlayerBuyWorkshopClickableConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(NotableSupportersCampaignBehavior), nameof(NotableSupportersCampaignBehavior.notable_support_request_on_condition))]
    [HarmonyPostfix]
    public static void NotableSupportRequestOnConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(NotableSupportersCampaignBehavior), nameof(NotableSupportersCampaignBehavior.notable_support_end_on_condition))]
    [HarmonyPostfix]
    public static void NotableSupportEndOnConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_want_to_join_faction_as_mercenary_or_vassal_on_condition))]
    [HarmonyPostfix]
    public static void JoinKingdomConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_is_offering_vassalage_while_at_mercenary_service_on_condition))]
    [HarmonyPostfix]
    public static void BecomeVassalConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_is_leaving_faction_on_condition))]
    [HarmonyPostfix]
    public static void LeaveKingdomConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_want_to_end_service_as_mercenary_on_condition))]
    [HarmonyPostfix]
    public static void EndMercenaryServiceConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_lord_request_mission_ask_on_condition))]
    [HarmonyPostfix]
    public static void MercenaryOfferConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    [HarmonyPatch(typeof(GovernorCampaignBehavior), nameof(GovernorCampaignBehavior.governor_talk_kingdom_creation_start_on_condition))]
    [HarmonyPostfix]
    public static void CreateKingdomConditionPostfix(ref bool __result)
        => CheckCanManageClan(ref __result);

    private static void CheckCanManageClan(ref bool __result)
    {
        if (!CoopClanPermissions.CanManageClan(Hero.MainHero.Clan))
        {
            __result = false;
        }
    }

    private static void CheckCanManageParty(ref bool __result)
    {
        if (!CoopClanPermissions.CanManageParty(Hero.OneToOneConversationHero?.PartyBelongedTo))
        {
            __result = false;
        }
    }
}
