using Common.Messaging;
using GameInterface.Services.CharacterCreation.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(CharacterCreationState))]
internal class CharacterCreationStatePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharacterCreationState.FinalizeCharacterCreation))]
    private static void FinalizeCharacterCreationPostfix(ref CharacterCreationState __instance)
    {
        MessageBroker.Instance.Publish(__instance, new CharacterCreationFinished());
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharacterCreationState.NextStage))]
    private static void NextStagePostfix(ref CharacterCreationState __instance)
    {
        if (__instance.CurrentStage is CharacterCreationOptionsStage)
        {
            __instance.FinalizeCharacterCreation();
        }
    }
}
