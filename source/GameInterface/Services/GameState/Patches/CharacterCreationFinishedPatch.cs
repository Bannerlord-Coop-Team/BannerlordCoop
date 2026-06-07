using Common.Messaging;
using GameInterface.Services.CharacterCreation.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using GameInterface.Policies;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(CharacterCreationState))]
internal class CharacterCreationFinishedPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("FinalizeCharacterCreationState")]
    private static void FinalizeCharacterCreation_Patch(ref CharacterCreationState __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        MessageBroker.Instance.Publish(__instance, new CharacterCreationFinished());
    }
}
