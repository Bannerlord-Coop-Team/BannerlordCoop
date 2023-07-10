using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static HarmonyLib.Code;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player encounters
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterManagerPatches
{
    private static MethodInfo Start => typeof(PlayerEncounter).GetMethod(nameof(PlayerEncounter.Start));
    private static MethodInfo Init => typeof(PlayerEncounter).GetMethod(
        "Init",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new Type[] { typeof(PartyBase), typeof(PartyBase), typeof(Settlement) },
        null);

    /// <summary>
    /// In the <see cref="EncounterManager.StartSettlementEncounter"/> method
    /// Replaces
    ///     PlayerEncounter.Start();
    ///     PlayerEncounter.Current.Init(attackerParty.Party, settlement.Party, settlement);
    /// With
    ///     EncounterManagerPatches.PlayerEncounterIntercept(attackerParty.Party, settlement.Party, settlement);
    /// </summary>
    /// <param name="instrs">previous instructions</param>
    /// <returns>patched instructions</returns>
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(EncounterManager.StartSettlementEncounter))]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instrs)
    {
        List<CodeInstruction> instructions = instrs.ToList();

        int startIdx = instructions.FindIndex(i => i.opcode == Call.opcode && i.operand as MethodInfo == Start);

        instructions.RemoveRange(startIdx, 2);

        int initIdx = instructions.FindIndex(i => i.opcode == Callvirt.opcode && i.operand as MethodInfo == Init);

        instructions[initIdx].opcode = Call.opcode;
        instructions[initIdx].operand = typeof(EncounterManagerPatches)
            .GetMethod(nameof(PlayerEncounterIntercept), BindingFlags.NonPublic | BindingFlags.Static);

        return instructions;
    }

    private static void PlayerEncounterIntercept(PartyBase attackerParty, PartyBase defenderParty, Settlement settlement)
    {
        var message = new StartSettlementEncounterAttempted(
            attackerParty.MobileParty.StringId,
            settlement.StringId);
        MessageBroker.Instance.Publish(attackerParty, message);
    }
}
