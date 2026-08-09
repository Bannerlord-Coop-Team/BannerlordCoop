using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(RebellionsCampaignBehavior))]
internal class RebellionsCampaignBehaviorPatches
{
    private static MethodInfo CreateSettlementRebelClanMethod => AccessTools.Method(
        typeof(Clan),
        nameof(Clan.CreateSettlementRebelClan),
        new[] { typeof(Settlement), typeof(Hero), typeof(int) });
    private static MethodInfo IsNobleSetter => AccessTools.PropertySetter(typeof(Clan), nameof(Clan.IsNoble));
    private static MethodInfo PublishRebelClanIsNobleMethod => AccessTools.Method(typeof(RebellionsCampaignBehaviorPatches), nameof(PublishRebelClanIsNoble));

    [HarmonyPatch(nameof(RebellionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(RebellionsCampaignBehavior.CreateRebelPartyAndClan))]
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> CreateRebelPartyAndClanTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var instructionList = new List<CodeInstruction>(instructions);
        int factoryCalls = 0;
        int replacements = 0;

        for (int index = 0; index < instructionList.Count; index++)
        {
            if (!instructionList[index].Calls(CreateSettlementRebelClanMethod)) continue;

            factoryCalls++;
            if (index + 4 >= instructionList.Count) continue;

            int storedLocal = GetStoredLocalIndex(instructionList[index + 1]);
            int loadedLocal = GetLoadedLocalIndex(instructionList[index + 2]);
            if (storedLocal < 0 || storedLocal != loadedLocal) continue;
            if (instructionList[index + 3].opcode != OpCodes.Ldc_I4_1) continue;
            if (!instructionList[index + 4].Calls(IsNobleSetter)) continue;

            instructionList[index + 4].opcode = OpCodes.Call;
            instructionList[index + 4].operand = PublishRebelClanIsNobleMethod;
            replacements++;
        }

        if (factoryCalls != 1 || replacements != 1)
        {
            throw new InvalidOperationException(
                $"Failed to patch rebel clan IsNoble snapshot: found {factoryCalls} factory calls and {replacements} adjacent setters.");
        }

        return instructionList;
    }

    private static int GetStoredLocalIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0) return 0;
        if (instruction.opcode == OpCodes.Stloc_1) return 1;
        if (instruction.opcode == OpCodes.Stloc_2) return 2;
        if (instruction.opcode == OpCodes.Stloc_3) return 3;
        if (instruction.opcode != OpCodes.Stloc && instruction.opcode != OpCodes.Stloc_S) return -1;

        return instruction.operand is LocalBuilder local ? local.LocalIndex : -1;
    }

    private static int GetLoadedLocalIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldloc_0) return 0;
        if (instruction.opcode == OpCodes.Ldloc_1) return 1;
        if (instruction.opcode == OpCodes.Ldloc_2) return 2;
        if (instruction.opcode == OpCodes.Ldloc_3) return 3;
        if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S) return -1;

        return instruction.operand is LocalBuilder local ? local.LocalIndex : -1;
    }

    internal static void PublishRebelClanIsNoble(Clan clan, bool isNoble)
    {
        clan.IsNoble = isNoble;

        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(clan, new SettlementRebelClanInitialized(clan));
        }
    }
}
