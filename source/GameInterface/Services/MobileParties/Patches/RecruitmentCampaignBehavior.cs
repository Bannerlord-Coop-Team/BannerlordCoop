using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class RecruitmentCampaignBehaviorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("CheckRecruiting")]
    private static bool CheckRecruitingPrefix(ref MobileParty mobileParty, ref Settlement settlement)
    {
        // TODO only allow for server and broadcast when it happens
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("HourlyTickParty")]
    private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty)
    {
        // TODO disable for player parties
        return false;
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement))]
    [HarmonyPostfix]
    public static void UpdateVolunteersOfNotablesInSettlementPostfix(Settlement settlement)
    {
        if ((settlement.IsTown && !settlement.Town.InRebelliousState) || (settlement.IsVillage && !settlement.Village.Bound.Town.InRebelliousState))
        {
            Dictionary<Hero, CharacterObject[]> updatedVolunteerTypes = new();
            foreach (Hero hero in settlement.Notables)
            {
                updatedVolunteerTypes[hero] = hero.VolunteerTypes;
            }

            // Need to manually sync the updated volunteer types
            var message = new VolunteersUpdated(updatedVolunteerTypes);
            MessageBroker.Instance.Publish(null, message);
        }
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.ApplyInternal))]
    [HarmonyPostfix]
    public static void ApplyInternalPostfix(Hero individual, int number, int bitCode, RecruitmentCampaignBehavior.RecruitingDetail detail)
    {
        if (detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual && detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividualToGarrison)
        {
            return;
        }

        // Need to manually sync the available volunteer types when removed on the server (e.g. by AI parties recruiting volunteers)
        var message = new VolunteerRemoved(individual, bitCode);
        MessageBroker.Instance.Publish(null, message);
    }
}