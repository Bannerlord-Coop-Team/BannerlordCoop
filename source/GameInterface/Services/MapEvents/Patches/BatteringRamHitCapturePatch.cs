using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// [Ram simulator] Reports each battering ram strike (the wind-up/hit swing) so everyone else can play the ram
/// body animation. That animation is a machine SynchedMissionObject animation which only broadcasts over the
/// inert GameNetwork, and a non-simulator's ram is unmanned (its TickAux is blocked) so it never strikes locally.
/// Rams are crew-grantable, so the gate is per-machine, not the mission host.
/// </summary>
[HarmonyPatch(typeof(BatteringRam), "StartHitAnimationWithProgress")]
internal static class BatteringRamHitCapturePatch
{
    private static void Postfix(BatteringRam __instance, int powerStage, float progress)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!SiegeMissionAuthorityGate.IsMachineSimulatedLocally(__instance.Id.Id)) return;

        MessageBroker.Instance.Publish(__instance, new RamHitStarted(__instance, powerStage, progress));
    }
}
