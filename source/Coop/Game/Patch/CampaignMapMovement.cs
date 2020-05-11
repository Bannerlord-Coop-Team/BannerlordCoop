using Coop.Sync;
using HarmonyLib;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Game.Patch
{
    [Patch]
    public static class CampaignMapMovement
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Field TargetPosition { get; } =
            new Field(AccessTools.Field(typeof(MobileParty), "_targetPosition"));

        [SyncWatch(typeof(MobileParty), nameof(MobileParty.TargetPosition), MethodType.Setter)]
        private static void Patch_GoToPoint(MobileParty __instance)
        {
            if (__instance == MobileParty.MainParty)
            {
                TargetPosition.Watch(__instance);
            }
        }

        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SetMoveEngageParty))]
        [HarmonyPatch(new[] {typeof(MobileParty)})]
        private static class MobileParty_SetMoveEngageParty
        {
            private static bool Prefix(MobileParty __instance, MobileParty party)
            {
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
