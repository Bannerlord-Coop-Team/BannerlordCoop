using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using System.Linq;

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

        static readonly List<string> args = Utilities.GetFullCommandLineString().Split(' ').ToList();

        [HarmonyPatch("ApplyInternal")] //TODO: Does this fire when other player parties recruit? If so, return false.
        public static bool Prefix(MobileParty side1Party, Settlement settlement, Hero individual, 
            CharacterObject troop, int number, int bitCode, RecruitingDetail detail)
        {
            if (!args.Contains("/server")) { return false; }

            MessageBroker.Instance.Publish(side1Party, new NetworkPartyRecruitUnit(
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
