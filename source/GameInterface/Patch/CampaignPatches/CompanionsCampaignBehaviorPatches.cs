using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Coop.Mod.Patch.CampaignPatches
{
    [HarmonyPatch(typeof(CompanionsCampaignBehavior))]
    internal class CompanionsCampaignBehaviorPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("CreateCompanionAndAddToSettlement")]
        private static bool Prefix()
        {
            return false;
        }
    }
}
