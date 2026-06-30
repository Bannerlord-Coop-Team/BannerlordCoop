using HarmonyLib;
using SandBox.View.Map.Visuals;
using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;

namespace GameInterface.Services.PartyVisuals.Patches;

// Party-icon agent-visuals are built with PrepareImmediately=false (AddCharacterToPartyIcon), so the
// engine prepares their meshes/skeletons asynchronously on a background resource-streaming thread. In
// co-op many icons rebuild in the same frames (replication bursts), so those in-flight prep jobs race
// the game-thread ResetPartyIcon teardown of the shared native mesh pool and corrupt it, surfacing as
// a native AccessViolation on an unrelated, fully-valid party. Forcing synchronous prep builds the
// meshes before Create returns, so nothing is left in flight to race.
[HarmonyPatch]
internal class PartyIconPreparePatches
{
    [ThreadStatic] private static bool _inRefresh;

    [HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.RefreshPartyIcon))]
    [HarmonyPrefix]
    private static void RefreshPrefix() => _inRefresh = true;

    [HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.RefreshPartyIcon))]
    [HarmonyFinalizer]
    private static void RefreshFinalizer() => _inRefresh = false;

    // Scope to agent-visuals built during a party-icon refresh (human + mount + caravan all go through
    // AgentVisuals.Create here); mission and UI agent-visuals are left untouched.
    [HarmonyPatch(typeof(AgentVisuals), nameof(AgentVisuals.Create),
        new[] { typeof(AgentVisualsData), typeof(string), typeof(bool), typeof(bool), typeof(bool) })]
    [HarmonyPrefix]
    private static void ForceSyncPrep(AgentVisualsData data)
    {
        if (_inRefresh && data != null)
            data.PrepareImmediately(true);
    }
}
