using Common.Messaging;
using GameInterface.Services.CharacterCreation.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(CharacterCreationState))]
internal class CharacterCreationFinishedPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("FinalizeCharacterCreation")]
    private static void FinalizeCharacterCreation_Patch(ref CharacterCreationState __instance)
    {
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, new CharacterCreationFinished());
    }
}
