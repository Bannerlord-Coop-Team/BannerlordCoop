using Common.Logging;
using HarmonyLib;
using NavalDLC.Missions.Objects.UsableMachines;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Safety net for a missed station release: a hull machine still seating a removed agent reads its freed native data on the next
/// tick, an access violation no finalizer catches, so in a co-op naval battle the seat is freed before the tick runs.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalRemovedStationUserPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalRemovedStationUserPatch>();

    // The hull machines whose own OnTick drives a seated agent through native calls without checking it is still active.
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(ShipControllerMachine), nameof(ShipControllerMachine.OnTick));
        yield return AccessTools.DeclaredMethod(typeof(ShipPullingMachine), nameof(ShipPullingMachine.OnTick));
        yield return AccessTools.DeclaredMethod(typeof(ShipAttachmentPointMachine), nameof(ShipAttachmentPointMachine.OnTick));
    }

    // Frees each standing point a removed agent still holds, then lets the tick run on the empty seat. The managed removal flags
    // are read because IsActive reads the agent's native state.
    [HarmonyPrefix]
    internal static void Prefix(UsableMachine __instance)
    {
        var points = __instance.StandingPoints;
        if (points == null) return;

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var user = point.UserAgent;
            if (user == null || !(user._isRemoved || user._isDeleted) || !NavalMissionTracker.IsActiveHere()) continue;

            // Vanilla's OnUseStopped and ClearTargetFrame call into the agent's freed data, so only the managed links are cut,
            // under the lock vanilla's own release takes.
            lock (Agent._stopUsingGameObjectLock)
            {
                if (!ReferenceEquals(point.UserAgent, user)) continue;

                if (ReferenceEquals(user.CurrentlyUsedGameObject, point))
                {
                    user.CurrentlyUsedGameObject = null;
                    user._usedObjectPreferenceIndex = -1;
                }
                point.UserAgent = null;
            }

            Logger.Warning("[NavalSync] {Machine} on {Hull} still seated removed agent {Agent} at standing point {Point}; a station release was missed, freed the seat",
                __instance.GetType().Name, __instance.GameEntity.IsValid ? __instance.GameEntity.Root.Name : null,
                user.Character?.StringId, i);
        }
    }
}
