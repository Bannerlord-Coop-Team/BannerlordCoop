using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(BattleSimulation))]
public class BattleSimulationUpdatePatch
{
    /// <summary>
    /// The simulation engine is authoritative on the server. Clients never run it locally
    /// (it rolls <c>MBRandom</c> and mutates rosters); instead they request the server to run it and
    /// replay the server's per-round results onto the scoreboard at the normal cadence. The server
    /// still ticks its own simulation normally.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(BattleSimulation.Tick))]
    static bool PrefixUpdate(BattleSimulation __instance, float dt)
    {
        if (ModInformation.IsServer)
            return true;

        BattleSimulationReplay.Tick(__instance, dt);
        return false;
    }

    [HarmonyPatch(typeof(MapState), nameof(MapState.EndBattleSimulation))]
    internal static class BattleSimulationEndPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            if (ModInformation.IsServer) return;

            if (BattleSimulationReplay.TryRequestCancellation(out var mapEventId))
            {
                MessageBroker.Instance.Publish(typeof(BattleSimulationEndPatch), new RequestCancelBattleSimulation(mapEventId));
            }
        }
    }
}

[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleRound))]
internal class MapEventSimulationRoundPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // Leaving any started encounter (village Leave included) runs one native round directly, bypassing
        // BattleSimulation.Tick. Clients never simulate, so that parting round is skipped in co-op.
        return CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateSingleTroopHit))]
internal class SimulatedTroopHitDifficultyPatch
{
    // Vanilla only applies the player troops received damage difficulty to hits on PartyBase.MainParty,
    // which the server doesn't have, so every player party took full auto resolve damage.
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var simulateHit = AccessTools.Method(typeof(CombatSimulationModel), nameof(CombatSimulationModel.SimulateHit), new[]
        {
            typeof(CharacterObject), typeof(CharacterObject), typeof(PartyBase), typeof(PartyBase),
            typeof(float), typeof(MapEvent), typeof(BattleEnvironment), typeof(float), typeof(float)
        });
        var getAllocatedTroopParty = AccessTools.Method(typeof(MapEventSide), nameof(MapEventSide.GetAllocatedTroopParty));
        int hitIndex = codes.FindIndex(instruction => instruction.Calls(simulateHit));
        if (hitIndex < 0)
            throw new InvalidOperationException("Failed to find the troop SimulateHit call in MapEvent.SimulateSingleTroopHit.");

        // The struck side's troop party is read last, right before the hit.
        int struckPartyStoreIndex = codes.FindLastIndex(hitIndex, instruction => instruction.Calls(getAllocatedTroopParty)) + 1;
        if (struckPartyStoreIndex <= 0 || !codes[struckPartyStoreIndex].IsStloc())
            throw new InvalidOperationException("Failed to find the struck party store in MapEvent.SimulateSingleTroopHit.");

        // Expected IL: int num = (int)SimulateHit(...).ResultNumber; then if (num > 0) as ldloc num, ldc.i4.0, ble.
        int damageStoreIndex = codes.FindIndex(hitIndex, instruction => instruction.opcode == OpCodes.Conv_I4) + 1;
        if (damageStoreIndex <= 0 || !codes[damageStoreIndex].IsStloc())
            throw new InvalidOperationException("Failed to find the damage store in MapEvent.SimulateSingleTroopHit.");

        int damageLocal = codes[damageStoreIndex].LocalIndex();
        int positiveDamageIndex = codes.FindIndex(damageStoreIndex, instruction => instruction.IsLdloc() && instruction.LocalIndex() == damageLocal);
        if (positiveDamageIndex < 0 || positiveDamageIndex + 2 >= codes.Count
            || !codes[positiveDamageIndex + 1].LoadsConstant(0)
            || (codes[positiveDamageIndex + 2].opcode != OpCodes.Ble && codes[positiveDamageIndex + 2].opcode != OpCodes.Ble_S))
            throw new InvalidOperationException("Failed to find the positive damage branch in MapEvent.SimulateSingleTroopHit.");

        // Scales inside vanilla's damage > 0 block, so the damage and the striker's hit reward both use it.
        codes.InsertRange(positiveDamageIndex + 3, new[]
        {
            CodeInstruction.LoadLocal(damageLocal),
            CodeInstruction.LoadLocal(codes[struckPartyStoreIndex].LocalIndex()),
            CodeInstruction.Call(() => ScalePlayerPartyDamage(default, default)),
            CodeInstruction.StoreLocal(damageLocal),
        });
        return codes;
    }

    private static int ScalePlayerPartyDamage(int damage, PartyBase struckParty)
    {
        // Clients never simulate, and one that did would already scale its own MainParty in vanilla.
        if (!ModInformation.IsServer || !struckParty.IsPlayerParty()) return damage;

        return MBRandom.RoundRandomized(damage * Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier());
    }
}

[HarmonyPatch(typeof(BattleSimulation), nameof(BattleSimulation.OnFinished))]
internal class BattleSimulationFinishedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsServer) return;

        // The scoreboard has closed, so the retained defeat can now end the encounter.
        if (ContainerProvider.TryResolve<IMapEventInitializationBarrier>(out var barrier))
            barrier.CompleteDeferredEncounterCleanup();
    }
}
