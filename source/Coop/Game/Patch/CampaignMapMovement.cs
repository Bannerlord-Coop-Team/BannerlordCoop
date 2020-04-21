using Coop.Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Patch
{
    public static class CampaignMapMovement
    {
        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveEngageParty))]
        [HarmonyPatch(new Type[] { typeof(MobileParty) })]
        private static class MobileParty_SetMoveEngageParty
        {
            static bool Prefix(MobileParty __instance, MobileParty party)
            {
                if(!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to engage {party}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoAroundParty))]
        [HarmonyPatch(new Type[] { typeof(MobileParty) })]
        private static class MobileParty_SetMoveGoAroundParty
        {
            static bool Prefix(MobileParty __instance, MobileParty party)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to move around {party}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoToSettlement))]
        [HarmonyPatch(new Type[] { typeof(Settlement) })]
        private static class MobileParty_SetMoveGoToSettlement
        {
            static bool Prefix(MobileParty __instance, Settlement settlement)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to move to {settlement}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoToPoint))]
        [HarmonyPatch(new Type[] { typeof(Vec2) })]
        private static class MobileParty_SetMoveGoToPoint
        {
            static bool Prefix(MobileParty __instance, Vec2 point)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to move to {point}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveEscortParty))]
        [HarmonyPatch(new Type[] { typeof(MobileParty) })]
        private static class MobileParty_SetMoveEscortParty
        {
            static bool Prefix(MobileParty __instance, MobileParty mobileParty)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to escort {mobileParty}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMovePatrolAroundPoint))]
        [HarmonyPatch(new Type[] { typeof(Vec2) })]
        private static class MobileParty_SetMovePatrolAroundPoint
        {
            static bool Prefix(MobileParty __instance, Vec2 point)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to patrol around {point}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMovePatrolAroundSettlement))]
        [HarmonyPatch(new Type[] { typeof(Settlement) })]
        private static class MobileParty_SetMovePatrolAroundSettlement
        {
            static bool Prefix(MobileParty __instance, Settlement settlement)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to patrol around {settlement}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveRaidSettlement))]
        [HarmonyPatch(new Type[] { typeof(Settlement) })]
        private static class MobileParty_SetMoveRaidSettlement
        {
            static bool Prefix(MobileParty __instance, Settlement settlement)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to move to raid {settlement}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveBesiegeSettlement))]
        [HarmonyPatch(new Type[] { typeof(Settlement) })]
        private static class MobileParty_SetMoveBesiegeSettlement
        {
            static bool Prefix(MobileParty __instance, Settlement settlement)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to move to besiege {settlement}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveDefendSettlement))]
        [HarmonyPatch(new Type[] { typeof(Settlement) })]
        private static class MobileParty_SetMoveDefendSettlement
        {
            static bool Prefix(MobileParty __instance, Settlement settlement)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to move to defend {settlement}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveModeHold))]
        private static class MobileParty_SetMoveModeHold
        {
            static bool Prefix(MobileParty __instance)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }
                Log.Trace($"{__instance} wants to hold.");
                return true;
            }
        }

        private static bool IsMovementRelevantForServer(MobileParty partyToMove)
        {
            return CoopClient.Instance.GameState.IsPlayerControlledParty(partyToMove);
        }
    }
}
