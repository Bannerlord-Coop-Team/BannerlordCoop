using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// [Ram simulator] Reports each battering ram hit on a gate, with its damage. A granted ram strikes only on
/// its simulator, so the host applies the carried damage to the authoritative gate and everyone else replays
/// the hit reaction (their gate's OnHit never runs). See SiegeWeaponFireReplicator's gate-hit handler.
/// </summary>
[HarmonyPatch(typeof(CastleGate), "OnHitTaken")]
internal static class CastleGateHitCapturePatch
{
    private static void Postfix(CastleGate __instance, ScriptComponentBehavior attackerScriptComponentBehavior, int inflictedDamage)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!(attackerScriptComponentBehavior is BatteringRam ram)) return;
        if (!SiegeMissionAuthorityGate.IsMachineSimulatedLocally(ram.Id.Id)) return;

        MessageBroker.Instance.Publish(__instance, new GateHitByRam(__instance, ram, inflictedDamage));
    }
}
