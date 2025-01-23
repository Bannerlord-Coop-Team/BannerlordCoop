using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        static bool PrefixUpdate(MapEvent __instance)
        {
            //To keep client "up to date" without running the Update method
            __instance.RecalculateStrengthOfSides();

            if (ModInformation.IsClient) return false;

            // Don't update if a player is involved
            // Prevents server from instantly finishing the battle and waits for client finish request
            if (__instance.InvolvedParties.Any(x => x.MobileParty.IsPartyControlled() == false)) return false;

            return true;
        }
    }
}
