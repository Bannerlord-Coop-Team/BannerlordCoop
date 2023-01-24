using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace GameInterface.Patch.GameStates
{
    [HarmonyPatch(typeof(CharacterCreationState))]
    internal class CharacterCreationPatch
    {
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPostfix]
        public static void CharacterCreationStateCtor(ref CharacterCreationState __instance)
        {
            //MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
        }
    }
}
