using Coop.Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Patch
{
    public static class CampaignMapMovement
    {
        private static bool IsMovementRelevantForServer(MobileParty partyToMove)
        {
            return CoopClient.Instance.GameState.IsPlayerControlledParty(partyToMove);
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveEngageParty))]
        [HarmonyPatch(new[] {typeof(MobileParty)})]
        private static class MobileParty_SetMoveEngageParty
        {
            private static bool Prefix(MobileParty __instance, MobileParty party)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }

                Log.Trace($"{__instance} wants to engage {party}.");
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoAroundParty))]
        [HarmonyPatch(new[] {typeof(MobileParty)})]
        private static class MobileParty_SetMoveGoAroundParty
        {
            private static bool Prefix(MobileParty __instance, MobileParty party)
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
        [HarmonyPatch(new[] {typeof(Settlement)})]
        private static class MobileParty_SetMoveGoToSettlement
        {
            private static bool Prefix(MobileParty __instance, Settlement settlement)
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
        [HarmonyPatch(new[] {typeof(Vec2)})]
        private static class MobileParty_SetMoveGoToPoint
        {
            private static bool Prefix(MobileParty __instance, Vec2 point)
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
        [HarmonyPatch(new[] {typeof(MobileParty)})]
        private static class MobileParty_SetMoveEscortParty
        {
            private static bool Prefix(MobileParty __instance, MobileParty mobileParty)
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
        [HarmonyPatch(new[] {typeof(Vec2)})]
        private static class MobileParty_SetMovePatrolAroundPoint
        {
            private static bool Prefix(MobileParty __instance, Vec2 point)
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
        [HarmonyPatch(new[] {typeof(Settlement)})]
        private static class MobileParty_SetMovePatrolAroundSettlement
        {
            private static bool Prefix(MobileParty __instance, Settlement settlement)
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
        [HarmonyPatch(new[] {typeof(Settlement)})]
        private static class MobileParty_SetMoveRaidSettlement
        {
            private static bool Prefix(MobileParty __instance, Settlement settlement)
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
        [HarmonyPatch(new[] {typeof(Settlement)})]
        private static class MobileParty_SetMoveBesiegeSettlement
        {
            private static bool Prefix(MobileParty __instance, Settlement settlement)
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
        [HarmonyPatch(new[] {typeof(Settlement)})]
        private static class MobileParty_SetMoveDefendSettlement
        {
            private static bool Prefix(MobileParty __instance, Settlement settlement)
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
            private static bool Prefix(MobileParty __instance)
            {
                if (!IsMovementRelevantForServer(__instance))
                {
                    return true;
                }

                Log.Trace($"{__instance} wants to hold.");
                return true;
            }
        }
    }
}
