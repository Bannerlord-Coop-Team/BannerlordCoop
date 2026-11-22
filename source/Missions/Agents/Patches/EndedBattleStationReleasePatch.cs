using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

/// <summary>
/// A co-op battle keeps its mission ticking after it ended, but vanilla <see cref="Agent.OnRemove"/> only releases a removed
/// agent's station before the end, so a machine kept ticking its removed user. The release runs after the end too.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.OnRemove))]
[HarmonyPatchCategory(MissionModule.PilotSeatPatchCategory)]
internal class EndedBattleStationReleasePatch
{
    // Releases the station the way OnRemove does before the end, while the native removal still holds the agent.
    private static void Prefix(Agent __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (__instance.Mission == null || !__instance.Mission.MissionEnded) return;
        if (!__instance.IsUsingGameObject && (__instance.HumanAIComponent == null || !__instance.InteractingWithAnyGameObject())) return;

        __instance.StopUsingGameObjectMT(isSuccessful: false, Agent.StopUsingGameObjectFlags.None);
    }
}
