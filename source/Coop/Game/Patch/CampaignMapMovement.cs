using Coop.Common;
using Coop.Game.Persistence;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Patch
{
    public static class CampaignMapMovement
    {
        public static IEnvironment s_Environment = null;
        public static bool s_IsRemoteUpdate = false;

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveEngageParty))]
        [HarmonyPatch(new[] {typeof(MobileParty)})]
        private static class MobileParty_SetMoveEngageParty
        {
            private static bool Prefix(MobileParty __instance, MobileParty party)
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoAroundParty))]
        [HarmonyPatch(new[] {typeof(MobileParty)})]
        private static class MobileParty_SetMoveGoAroundParty
        {
            private static bool Prefix(MobileParty __instance, MobileParty party)
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoToSettlement))]
        [HarmonyPatch(new[] {typeof(Settlement)})]
        private static class MobileParty_SetMoveGoToSettlement
        {
            private static bool Prefix(MobileParty __instance, Settlement settlement)
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveGoToPoint))]
        [HarmonyPatch(new[] {typeof(Vec2)})]
        private static class MobileParty_SetMoveGoToPoint
        {
            private static bool Prefix(MobileParty __instance, Vec2 point)
            {
                if (s_IsRemoteUpdate || s_Environment == null)
                {
                    return true;
                }

                if (s_Environment.RemoteMoveTo.TryGetValue(__instance, out RemoteValue<Vec2> val))
                {
                    Log.Trace($"{__instance} wants to move to {point}.");
                    val.Request(point);
                    return false;
                }

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
                return true;
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveModeHold))]
        private static class MobileParty_SetMoveModeHold
        {
            private static bool Prefix(MobileParty __instance)
            {
                return true;
            }
        }
    }
}
