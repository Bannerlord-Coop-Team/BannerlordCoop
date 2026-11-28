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
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

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
    // Unlike the prefixes above, this swaps only that check, so vanilla's own multiplier still does the scaling.
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var isPlayerSimulation = AccessTools.PropertyGetter(typeof(MapEvent), nameof(MapEvent.IsPlayerSimulation));
        var mainParty = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.MainParty));
        var getAllocatedTroopParty = AccessTools.Method(typeof(MapEventSide), nameof(MapEventSide.GetAllocatedTroopParty));

        // Expected IL: if (IsPlayerSimulation && struckParty == PartyBase.MainParty) as
        // ldarg.0, call IsPlayerSimulation, brfalse skip, ldloc struckParty, call MainParty, bne.un skip.
        int checkIndex = codes.FindIndex(instruction => instruction.Calls(isPlayerSimulation));
        bool isEligibilityCheck = checkIndex > 0 && checkIndex + 4 < codes.Count
            && codes[checkIndex - 1].IsLdarg(0)
            && new[] { OpCodes.Brfalse, OpCodes.Brfalse_S }.Contains(codes[checkIndex + 1].opcode)
            && codes[checkIndex + 2].IsLdloc()
            && codes[checkIndex + 3].Calls(mainParty)
            && new[] { OpCodes.Bne_Un, OpCodes.Bne_Un_S }.Contains(codes[checkIndex + 4].opcode)
            // Harmony gives each branch its own label, so both skips must land on the same instruction.
            && codes.Any(code => code.labels.Contains((Label)codes[checkIndex + 1].operand) && code.labels.Contains((Label)codes[checkIndex + 4].operand));
        if (!isEligibilityCheck)
            throw new InvalidOperationException("Failed to find the player damage difficulty check in MapEvent.SimulateSingleTroopHit.");

        var skipLabel = codes[checkIndex + 1].operand;
        // The struck side's troop party is the last one read before the check.
        int struckLocal = codes[checkIndex + 2].LocalIndex();
        int struckPartyStoreIndex = codes.FindLastIndex(checkIndex, instruction => instruction.Calls(getAllocatedTroopParty)) + 1;
        if (struckPartyStoreIndex <= 0 || !codes[struckPartyStoreIndex].IsStloc() || codes[struckPartyStoreIndex].LocalIndex() != struckLocal)
            throw new InvalidOperationException("Failed to find the struck party in MapEvent.SimulateSingleTroopHit.");

        // Vanilla's ldarg.0 stays as the map event argument of the new check.
        codes.RemoveRange(checkIndex, 5);
        codes.InsertRange(checkIndex, new[]
        {
            CodeInstruction.LoadLocal(struckLocal),
            CodeInstruction.Call(() => ScalesReceivedDamage(default, default)),
            new CodeInstruction(OpCodes.Brfalse, skipLabel),
        });
        return codes;
    }

    private static bool ScalesReceivedDamage(MapEvent mapEvent, PartyBase struckParty)
    {
        // The server has no MainParty of its own, so there every player party gets the difficulty.
        if (ModInformation.IsServer) return struckParty.IsPlayerParty();

        return mapEvent.IsPlayerSimulation && struckParty == PartyBase.MainParty;
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
