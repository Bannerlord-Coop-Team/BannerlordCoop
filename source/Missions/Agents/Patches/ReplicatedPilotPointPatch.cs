using GameInterface;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

[HarmonyPatch(typeof(StandingPoint), "TickAux")]
[HarmonyPatchCategory(MissionModule.PilotSeatPatchCategory)]
internal class ReplicatedPilotPointPatch
{
    private static bool Prefix(StandingPoint __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive || __instance.UserAgent == null)
            return true;
        // The owner's action/equipment stream owns this puppet, including automatic sheathing and release.
        return !ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry) ||
            !registry.TryGetAgentInfo(__instance.UserAgent, out var info) ||
            info.ReplicatedPilotPoint != __instance;
    }
}
