#if DEBUG
using HarmonyLib;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;

namespace Missions.Naval;

internal static class NavalLabNativePatches
{
    [HarmonyPatch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.PlayerControlledShip), MethodType.Getter)]
    private static class OwnerShipSelection
    {
        private static void Postfix(NavalShipsLogic __instance, ref MissionShip __result)
        {
            var active = NavalLabPhysicsPatches.Active;
            if (active != null && active.Mission == __instance.Mission)
                __result = active.GetLocalControlledShip();
        }
    }

    [HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.ManageShipDetachments))]
    private static class FixedInitialOarAllocation
    {
        private static bool Prefix(ShipOrder __instance) => NavalLabPhysicsPatches.Active?.OwnsFixedStationOrder(__instance) != true;
    }
}
#endif
