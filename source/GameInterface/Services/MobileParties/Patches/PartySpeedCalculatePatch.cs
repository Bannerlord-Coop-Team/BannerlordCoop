using System;
using System.Collections.Generic;
using System.Text;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(MobileParty))]
    internal class PartySpeedCalculatePatch
    {

        [HarmonyPrefix]
        [HarmonyPatch("CalculateSpeed")]
        static void CalculateSpeed(ref MobileParty __instance/*, ref float __result*/)
        {
            if(__instance.IsPartyControlled() && __instance.ToString().Contains("CoopParty"))
            {
                String s = __instance.ToString();
                int i = 0;
            }
            //__result = 0.0f;

        }

    }
}
