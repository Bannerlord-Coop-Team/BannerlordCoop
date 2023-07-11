using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
    public class UnitRecruitPatch
    {
        [HarmonyPatch("OnUnitRecruited")]
        public static void Prefix(CharacterObject troop, int count)
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new OnUnitRecruited(troop.StringId, count, MobileParty.MainParty.StringId));
        }

        [HarmonyPatch("ApplyInternal")] //TODO: Does this fire when other player parties recruit? If so, return false.
        public static bool Prefix(MobileParty side1Party, Settlement settlement, Hero individual, 
            CharacterObject troop, int number, int bitCode, RecruitingDetail detail)
        {
            if (ModInformation.IsClient) { return false; }

            MessageBroker.Instance.Publish(side1Party, new PartyRecruitUnit(
                side1Party.StringId, 
                settlement?.StringId, 
                individual?.StringId, 
                troop.StringId, 
                number,
                bitCode,
                Convert.ToInt16(detail)
                ));

            return true;
        }
    }
}
