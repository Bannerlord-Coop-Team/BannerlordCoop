using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(
    typeof(DefaultTargetScoreCalculatingModel),
    nameof(DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction))]
internal class PlayerSiegeTargetScoringPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerSiegeTargetScoringPatches>();

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        MethodInfo mainHeroGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MainHero));
        MethodInfo currentSettlementGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CurrentSettlement));
        MethodInfo applyAdjustment = AccessTools.Method(
            typeof(PlayerSiegeTargetScoringPatches),
            nameof(ApplyPlayerSettlementDefense));

        int playerPresenceIndex = FindPlayerPresenceCalculation(
            codes,
            mainHeroGetter,
            currentSettlementGetter);
        if (playerPresenceIndex < 4)
            throw new InvalidOperationException(
                "Failed to find the player-presence calculation in DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction.");

        CodeInstruction totalStrengthStore = codes[playerPresenceIndex - 3];
        CodeInstruction mobileLordStrengthStore = codes[playerPresenceIndex - 1];
        if (!IsStoreLocal(totalStrengthStore) || !IsStoreLocal(mobileLordStrengthStore))
            throw new InvalidOperationException(
                "Failed to resolve the settlement-defense locals in DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction.");

        int endFinallyIndex = codes.FindIndex(
            playerPresenceIndex + 4,
            instruction => instruction.opcode == OpCodes.Endfinally);
        if (endFinallyIndex < 0 || endFinallyIndex + 1 >= codes.Count)
            throw new InvalidOperationException(
                "Failed to find the end of the settlement-defense loop in DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction.");

        int insertionIndex = endFinallyIndex + 1;
        CodeInstruction insertionTarget = codes[insertionIndex];
        var loadSettlement = new CodeInstruction(OpCodes.Ldarg_1);
        loadSettlement.labels.AddRange(insertionTarget.labels);
        loadSettlement.blocks.AddRange(insertionTarget.blocks);
        insertionTarget.labels.Clear();
        insertionTarget.blocks.Clear();

        codes.InsertRange(insertionIndex, new[]
        {
            loadSettlement,
            new CodeInstruction(OpCodes.Ldloca_S, totalStrengthStore.operand),
            new CodeInstruction(OpCodes.Ldloca_S, mobileLordStrengthStore.operand),
            new CodeInstruction(OpCodes.Call, applyAdjustment)
        });

        // The dedicated server has no MainHero; the totals are replaced after the loop.
        var noLocalMainHero = new CodeInstruction(OpCodes.Ldc_I4_0);
        for (int i = 0; i < 4; i++)
        {
            noLocalMainHero.labels.AddRange(codes[playerPresenceIndex + i].labels);
            noLocalMainHero.blocks.AddRange(codes[playerPresenceIndex + i].blocks);
        }

        codes.RemoveRange(playerPresenceIndex, 4);
        codes.Insert(playerPresenceIndex, noLocalMainHero);
        return codes;
    }

    private static int FindPlayerPresenceCalculation(
        IReadOnlyList<CodeInstruction> codes,
        MethodInfo mainHeroGetter,
        MethodInfo currentSettlementGetter)
    {
        for (int i = 0; i <= codes.Count - 4; i++)
        {
            if (codes[i].Calls(mainHeroGetter) &&
                codes[i + 1].Calls(currentSettlementGetter) &&
                codes[i + 2].opcode == OpCodes.Ldarg_1 &&
                codes[i + 3].opcode == OpCodes.Ceq)
                return i;
        }

        return -1;
    }

    private static bool IsStoreLocal(CodeInstruction instruction)
        => (instruction.opcode == OpCodes.Stloc || instruction.opcode == OpCodes.Stloc_S) &&
           instruction.operand is LocalBuilder;

    private static void ApplyPlayerSettlementDefense(
        Settlement targetSettlement,
        ref float totalStrength,
        ref float mobileLordStrength)
    {
        if (!ContainerProvider.TryResolve<IPlayerSiegeTargetScoring>(out var scoring))
        {
            Logger.Error("Unable to resolve {Scoring}", nameof(IPlayerSiegeTargetScoring));
            return;
        }

        SettlementDefenseScore score = scoring.CalculateSettlementDefense(targetSettlement);
        totalStrength = score.TotalStrength;
        mobileLordStrength = score.MobileLordStrength;
    }
}
