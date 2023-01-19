using HarmonyLib;
using Common.Messaging;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using GameInterface.Services.GameDebug.Messages;

namespace GameInterface.Services.GameState.Patches
{
    [HarmonyPatch(typeof(CharacterCreationState))]
    internal class CharacterCreationPatch
    {
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPostfix]
        public static void CharacterCreationStateCtor(ref CharacterCreationState __instance)
        {
            MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
        }
    }
}
