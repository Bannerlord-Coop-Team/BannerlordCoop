using Common;
using Coop.Naval.CampaignBehaviors.Interfaces;
using GameInterface;
using GameInterface.Services.Clans;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ClanFleetManagementCampaignBehavior))]
internal class ClanFleetManagementCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ClanFleetManagementCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    public static bool HourlyTickPrefix()
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IClanFleetManagementCampaignBehaviorInterface>(out var campaignBehaviorInterface)) return false;

        // Vanilla returns MobileParty.MainParty's troops, the server returns each player's
        campaignBehaviorInterface.HourlyTick();

        return false;
    }

    [HarmonyPatch(nameof(ClanFleetManagementCampaignBehavior.SendShipToParty))]
    [HarmonyPrefix]
    public static bool SendShipToPartyPrefix(Ship ship, MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IClanFleetManagementCampaignBehaviorInterface>(out var campaignBehaviorInterface)) return false;

        // Vanilla takes the troops from MobileParty.MainParty instead of the player sending the ship
        campaignBehaviorInterface.SendShipToParty(ship, mobileParty);

        return false;
    }

    // Same gate as managing a clan member's inventory and troops, otherwise any clan member could take another party's ships
    [HarmonyPatch(nameof(ClanFleetManagementCampaignBehavior.conversation_clan_member_manage_fleet_on_condition))]
    [HarmonyPostfix]
    public static void ConversationClanMemberManageFleetOnConditionPostfix(ref bool __result)
    {
        if (!CoopClanPermissions.CanManageParty(Hero.OneToOneConversationHero?.PartyBelongedTo))
        {
            __result = false;
        }  
    }
}
