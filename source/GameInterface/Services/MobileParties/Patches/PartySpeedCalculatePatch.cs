using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(MobileParty))]
    internal class PartySpeedCalculatePatch
    {
        //private static readonly AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();
        private static DateTime startTime = DateTime.UtcNow;
        // may want post so we can get the speed the player calculated.
        [HarmonyPostfix]
        [HarmonyPatch("CalculateSpeed")]
        static void CalculateSpeed(ref MobileParty __instance, ref float __result)
        {

            if (!ModInformation.IsServer) return;

            if (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(500)) return;

            // There is probably a better manner to check if its a player. for now will suffice.
            if (__instance.ToString().Contains("CoopParty"))
            {
                MessageBroker.Instance.Publish(__instance, new CalculateSpeed(__result));
            }


            startTime = DateTime.UtcNow;
        }
    }
}
