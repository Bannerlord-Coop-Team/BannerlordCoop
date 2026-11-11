using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using NavalDLC.Missions.Objects.UsableMachines;

namespace Missions.Naval;

// ShipBallista overrides ShootProjectileAux without calling the base, so the shared siege capture never sees its launch;
// fire ballistas inherit it. ShipMangonel keeps the base method, which the shared capture already brackets.
[HarmonyPatch(typeof(ShipBallista), nameof(ShipBallista.ShootProjectileAux))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipBallistaShotCapturePatch
{
    // Opens the shot's capture context for this ballista.
    [HarmonyPrefix]
    private static void Prefix(ShipBallista __instance) => SiegeWeaponFireContext.Begin(__instance);

    // Closes the capture context, also when the shot throws.
    [HarmonyFinalizer]
    private static void Finalizer() => SiegeWeaponFireContext.End();
}
