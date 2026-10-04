using Common.Messaging;
using HarmonyLib;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// Every NavalShipsLogic spawn funnels through the formation overload; announce it so the replicator can register own hulls.
[HarmonyPatch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.SpawnShip),
    new[] { typeof(Formation), typeof(MatrixFrame), typeof(bool), typeof(bool) },
    new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalShipSpawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(MissionShip __result, Formation formation)
    {
        if (__result == null) return;

        MessageBroker.Instance.Publish(__result, new ShipSpawnedInBattle(__result, formation));
    }
}

// Only the owner runs a hull's ship order; a foreign copy follows the owner's frames.
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.Tick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignShipOrderTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipOrder __instance) => !NavalForeignHulls.Contains(__instance._ownerShip);
}

// Detachments move crew onto machines; on a foreign copy that is the owner's decision.
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.ManageShipDetachments))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignShipDetachmentsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipOrder __instance) => !NavalForeignHulls.Contains(__instance._ownerShip);
}

// Vanilla hands every non-player hull to AI when deployment ends; foreign copies stay uncontrolled.
[HarmonyPatch(typeof(DefaultNavalMissionLogic), nameof(DefaultNavalMissionLogic.OnDeploymentFinished))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignHullControllerPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        foreach (var ship in NavalForeignHulls.All)
            ship.SetController(ShipControllerType.None, autoUpdateController: false);
    }
}
