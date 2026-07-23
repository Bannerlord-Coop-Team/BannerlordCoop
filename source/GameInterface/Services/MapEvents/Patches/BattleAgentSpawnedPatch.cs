using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Captures every agent this client spawns into a coop field battle (postfix on the single
/// <see cref="Mission.SpawnAgent"/> chokepoint) and publishes <see cref="AgentSpawnedInBattle"/> so the
/// Missions battle controller can register it under this client and replicate it to peers. Each client
/// spawns only the troops it owns: its server-fed <c>CoopTroopSupplier</c> serves only its own party (plus
/// the AI/enemy side on the host), so every captured agent is owned by the local controller.
/// </summary>
// Target the single overload explicitly. Mission.SpawnAgent has one overload today, but pinning the
// parameter types keeps a future engine overload from making AccessTools.Method ambiguous — an
// AmbiguousMatchException in PatchAll would abort every GameInterface patch, not just this one.
[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool) })]
internal class BattleAgentSpawnedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __result)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        // A puppet being spawned from another owner's broadcast is NOT our troop — don't re-capture it.
        if (BattleSpawnGate.SuppressCapture) return;
        if (__result == null) return;

        MessageBroker.Instance.Publish(__result, new AgentSpawnedInBattle(__result));
    }
}
