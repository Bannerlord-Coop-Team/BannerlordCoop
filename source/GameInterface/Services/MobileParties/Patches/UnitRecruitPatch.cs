using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
    public class UnitRecruitPatch
    {
        [HarmonyPatch("OnUnitRecruited")]
        public static bool Prefix(CharacterObject troop, int count)
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new OnUnitRecruited(troop.StringId, count, MobileParty.MainParty.StringId));

            return true;
        }
    }
}
