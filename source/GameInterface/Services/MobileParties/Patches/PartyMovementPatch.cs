using GameInterface.Services.MobileParties.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(MobileParty))]
    internal class PartyMovementPatch
    {
        private static MobileParty AllowedChangeParty;

        [HarmonyPrefix]
        [HarmonyPatch("TargetPosition")]
        [HarmonyPatch(MethodType.Setter)]
        private static bool MovementPrefix(ref MobileParty __instance)
        {
            if (MobilePartyInterface.ControlledParties.Contains(__instance))
            {
                // If controlled parties position is allowed to be change
                // Allow setting
                if(__instance == AllowedChangeParty) return true;

                // If party is not allowed, do not allow setting
                return false;
            }

            // Allow if party is not controlled
            return true;
        }

        internal static readonly PropertyInfo MobileParty_TargetPosition = typeof(MobileParty).GetProperty(nameof(MobileParty.TargetPosition));
        public static void SetTargetPositionOverride(MobileParty party, ref Vec2 position)
        {
            AllowedChangeParty = party;
            lock (AllowedChangeParty)
            {
                MobileParty_TargetPosition.SetValue(party, position);
            }    
        }
    }
}
