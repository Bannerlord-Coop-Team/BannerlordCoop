using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal class SharedClanDialoguePatches
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

    private static void CheckCanManageClan(ref bool __result)
    {
        if (!SharedClanPermissions.CanManageClan(Hero.MainHero.Clan))
        {
            __result = false;
        }
    }

    private static void CheckCanManageParty(ref bool __result)
    {
        if (!SharedClanPermissions.CanManageParty(MobileParty.ConversationParty))
        {
            __result = false;
        }
    }
}
