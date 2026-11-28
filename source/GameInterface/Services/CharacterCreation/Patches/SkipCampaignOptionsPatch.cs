using Common;
using GameInterface.Services.AdvancedStart;
using GameInterface.Services.AdvancedStart.Interfaces;
using HarmonyLib;
using SandBox.GauntletUI.CharacterCreation;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Localization;

namespace GameInterface.Services.CharacterCreation.Patches;

/// <summary>
/// Skip adding the options stage to the character creation on clients
/// </summary>
[HarmonyPatch(typeof(CharacterCreationManager))]
internal class SkipCampaignOptionsPatch
{
    [HarmonyPatch(nameof(CharacterCreationManager.AddStage))]
    [HarmonyPrefix]
    public static bool AddStagePrefix(CharacterCreationManager __instance, CharacterCreationStageBase stage)
    {
        return ModInformation.IsServer || stage.GetType() != typeof(CharacterCreationOptionsStage);
    }
}

/// <summary>
/// Replaces "Next" text in the review stage to show client this is the last stage before joining,
/// unless the server's starting options window comes next
/// </summary>
[HarmonyPatch(typeof(CharacterCreationReviewStageView), MethodType.Constructor,
    new[] { typeof(CharacterCreationManager), typeof(ControlCharacterCreationStage),
    typeof(TextObject), typeof(ControlCharacterCreationStage),
    typeof(TextObject), typeof(ControlCharacterCreationStage),
    typeof(ControlCharacterCreationStageReturnInt),
    typeof(ControlCharacterCreationStageReturnInt),
    typeof(ControlCharacterCreationStageReturnInt),
    typeof(ControlCharacterCreationStageWithInt) })]
internal static class ReplaceCharacterReviewStageTextPatch
{
    private const string NextText = CoopCampaignStartingOptionsVM.NextLabel;

    private static readonly MethodInfo GetAffirmativeButtonTextMethod = AccessTools.Method(typeof(ReplaceCharacterReviewStageTextPatch), nameof(GetAffirmativeButtonText));

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand as string == NextText)
            {
                yield return new CodeInstruction(OpCodes.Call, GetAffirmativeButtonTextMethod);

                continue;
            }

            yield return instruction;
        }
    }

    private static string GetAffirmativeButtonText()
    {
        if (ModInformation.IsServer) return NextText;

        if (ContainerProvider.TryResolve<IAdvancedStartInterface>(out var advancedStartInterface) &&
            advancedStartInterface.WillShowStartOptions())
            return NextText;

        return CoopCampaignStartingOptionsVM.JoinLabel;
    }
}