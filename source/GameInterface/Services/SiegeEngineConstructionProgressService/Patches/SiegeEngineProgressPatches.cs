using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.SiegeEngines;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using HarmonyLib;
using Serilog;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Patches;

/// <summary>
/// Threshold sync for the two per-tick construction values. The server's ConstructionTick calls
/// SetProgress/SetRedeploymentProgress every campaign tick, so publishing every set would flood the
/// network; a whole-percent bucket change bounds it to at most 100 messages per engine build while
/// still delivering the exact 1f completion value clients gate menus on.
/// </summary>
[HarmonyPatch(typeof(SiegeEngineConstructionProgress))]
internal class SiegeEngineProgressPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineProgressPatches>();

    [HarmonyPatch(nameof(SiegeEngineConstructionProgress.SetProgress))]
    [HarmonyPrefix]
    private static bool SetProgressPrefix(SiegeEngineConstructionProgress __instance, float progress)
    {
        return HandleSet(__instance, __instance.Progress, progress, isRedeployment: false);
    }

    [HarmonyPatch(nameof(SiegeEngineConstructionProgress.SetRedeploymentProgress))]
    [HarmonyPrefix]
    private static bool SetRedeploymentProgressPrefix(SiegeEngineConstructionProgress __instance, float redeploymentProgress)
    {
        return HandleSet(__instance, __instance.RedeploymentProgress, redeploymentProgress, isRedeployment: true);
    }

    // Hitpoints replicate here (register-then-broadcast, applied on the game thread) rather than through the
    // generic property auto-sync, which dropped values set before an engine had a network id. The property
    // setter is hooked (not the SetHitpoints method) so the constructor's direct "Hitpoints = MaxHitPoints"
    // initial assignment is caught too, not just battle damage.
    [HarmonyPatch(nameof(SiegeEngineConstructionProgress.Hitpoints), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool HitpointsSetterPrefix(SiegeEngineConstructionProgress __instance, float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set siege engine hitpoints outside a synced flow");
            return false;
        }

        if (__instance.Hitpoints != value)
        {
            SiegeEngineRegistration.EnsureRegistered(__instance, nameof(SiegeEngineConstructionProgress.Hitpoints));
            // MaxHitPoints is assigned just before Hitpoints in the constructor, so it is already current here.
            MessageBroker.Instance.Publish(__instance, new SiegeEngineHitpointsChanged(__instance, value, __instance.MaxHitPoints));
        }

        return true;
    }

    private static bool HandleSet(SiegeEngineConstructionProgress instance, float oldValue, float newValue, bool isRedeployment)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set siege engine progress outside a synced flow");
            return false;
        }

        if ((int)(oldValue * 100f) != (int)(newValue * 100f))
        {
            SiegeEngineRegistration.EnsureRegistered(instance, isRedeployment ? nameof(SiegeEngineConstructionProgress.SetRedeploymentProgress) : nameof(SiegeEngineConstructionProgress.SetProgress));
            MessageBroker.Instance.Publish(instance, new SiegeEngineProgressChanged(instance, isRedeployment, newValue));
        }

        return true;
    }

    internal static void RunSetProgress(SiegeEngineConstructionProgress siegeEngine, bool isRedeployment, float value)
    {
        bool completed = value >= 1f && (isRedeployment ? siegeEngine.RedeploymentProgress : siegeEngine.Progress) < 1f;

        using (new AllowedThread())
        {
            if (isRedeployment)
            {
                siegeEngine.SetRedeploymentProgress(value);
            }
            else
            {
                siegeEngine.SetProgress(value);
            }
        }

        // Vanilla re-renders the besieged settlement when a construction completes, from the server-only siege
        // tick. The client applies progress silently, so without this dirty the prep-complete platforms (and a
        // finished engine's mesh) never show until something else happens to dirty the settlement visual.
        if (completed)
        {
            SiegeContainerLookup.FindOwnerSettlement(siegeEngine)?.Party?.SetVisualAsDirty();
        }
    }

    internal static void RunSetHitpoints(SiegeEngineConstructionProgress siegeEngine, float hitpoints, float maxHitPoints)
    {
        using (new AllowedThread())
        {
            // MaxHitPoints has no setter method; the publicizer exposes its private setter, and it is no longer
            // auto-synced, so assigning it directly here does not re-trigger a patch.
            siegeEngine.MaxHitPoints = maxHitPoints;
            siegeEngine.SetHitpoints(hitpoints);
        }
    }
}
