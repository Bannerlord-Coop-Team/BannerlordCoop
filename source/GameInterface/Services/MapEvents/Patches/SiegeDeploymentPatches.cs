using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions;
using TaleWorlds.MountAndBlade.Missions.Handlers;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Single-deployer siege engine placement. Vanilla auto-places both sides' engines per machine with
/// unseeded RNG, so every client would end up with a physically different layout of rams, towers and
/// ballistas — objects agents path to and operate. In a coop siege only the mission host runs the
/// auto-deploys and the engine UI; its Deploy/Disband calls replicate over the mesh and everyone else
/// applies them.
/// </summary>
[HarmonyPatch]
internal class SiegeDeploymentPatches
{
    [HarmonyPatch(typeof(SiegeDeploymentHandler), nameof(SiegeDeploymentHandler.DeployAllSiegeWeaponsOfPlayer))]
    [HarmonyPrefix]
    private static bool DeployAllOfPlayerPrefix()
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;

        return SiegeMissionAuthorityGate.IsLocalAuthority;
    }

    [HarmonyPatch(typeof(SiegeDeploymentHandler), nameof(SiegeDeploymentHandler.DeployAllSiegeWeaponsOfAi))]
    [HarmonyPrefix]
    private static bool DeployAllOfAiPrefix()
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;

        return SiegeMissionAuthorityGate.IsLocalAuthority;
    }

    // Empties the engine list of the deployment UI (and the "you can still deploy" prompt) for
    // non-deployers; their troop Order of Battle is untouched.
    [HarmonyPatch(typeof(SiegeDeploymentHandler), nameof(SiegeDeploymentHandler.GetMaxDeployableWeaponCountOfPlayer))]
    [HarmonyPostfix]
    private static void GetMaxDeployableCountPostfix(ref int __result)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;

        if (!SiegeMissionAuthorityGate.IsLocalAuthority)
        {
            __result = 0;
        }
    }

    // Both Deploy overloads and Disband set DeployedWeapon and then funnel through this method, so
    // one postfix covers every placement change.
    [HarmonyPatch(typeof(DeploymentPoint), nameof(DeploymentPoint.OnDeploymentStateChangedAux))]
    [HarmonyPostfix]
    private static void DeploymentStateChangedPostfix(DeploymentPoint __instance)
    {
        PublishPlacement(__instance);
    }

    private static void PublishPlacement(DeploymentPoint point)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (SiegeMissionAuthorityGate.SuppressCapture) return;
        if (!SiegeMissionAuthorityGate.IsLocalAuthority) return;

        string weaponTypeName = point.DeployedWeapon != null
            ? MissionSiegeWeaponsController.GetWeaponType(point.DeployedWeapon).Name
            : null;

        MessageBroker.Instance.Publish(point, new SiegeEnginePlacementChanged(point, weaponTypeName));
    }
}
