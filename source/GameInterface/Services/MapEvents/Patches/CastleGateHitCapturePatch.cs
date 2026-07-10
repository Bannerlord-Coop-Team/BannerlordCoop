using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// [Host] Reports when a battering ram struck a gate hard enough to trigger the gate's hit reaction, so peers
/// can replay it (their gate's OnHit never runs, so its OnHitTaken handler — the door flinch + impact sound —
/// never fires). The condition mirrors CastleGate.OnHitTaken so peers react exactly when the host does.
/// </summary>
[HarmonyPatch(typeof(CastleGate), "OnHitTaken")]
internal static class CastleGateHitCapturePatch
{
    private static void Postfix(CastleGate __instance, ScriptComponentBehavior attackerScriptComponentBehavior, int inflictedDamage)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!SiegeMissionAuthorityGate.IsLocalAuthority) return;

        if (inflictedDamage >= 200 && __instance.State == CastleGate.GateState.Closed && attackerScriptComponentBehavior is BatteringRam)
        {
            MessageBroker.Instance.Publish(__instance, new GateHitByRam(__instance));
        }
    }
}
