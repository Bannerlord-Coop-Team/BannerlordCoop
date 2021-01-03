using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.ComputeIsWaiting))]
    class ComputeIsWaitingPatch
    {

        [HarmonyPrefix]
        static bool Prefix(MobileParty __instance, ref bool __result)
        {
            if (__instance.IsMainParty)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
