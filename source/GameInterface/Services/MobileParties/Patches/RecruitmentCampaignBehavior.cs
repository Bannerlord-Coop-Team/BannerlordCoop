using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
public class RecruitmentCampaignBehaviorPatch
{
    static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement)
    {
        // TODO only allow for server and broadcast when it happens
        return true;
    }

    //[HarmonyPrefix]
    //[HarmonyPatch("HourlyTickParty")]
    //private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty)
    //{
    //    // TODO disable for player parties
    //    return false;
    //}

    //[HarmonyPrefix]
    //[HarmonyPatch("OnUnitRecruited")]
    //static bool Prefix(CharacterObject troop, int count)
    //{

    //    return true;
    //}

    //[HarmonyPrefix]
    //[HarmonyPatch("OnSettlementEntered")]
    //static bool Prefix(MobileParty mobileParty, Settlement settlement, Hero hero)
    //{
    //    return true;
    //}
}