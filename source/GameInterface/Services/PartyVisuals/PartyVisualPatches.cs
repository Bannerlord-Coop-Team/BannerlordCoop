using HarmonyLib;
using SandBox.View.Map;

namespace GameInterface.Services.PartyVisuals
{
    [HarmonyPatch]
    internal class PartyVisualDisableClient
    {
        [HarmonyPatch(typeof(PartyVisual), nameof(PartyVisual.AddCharacterToPartyIcon))]
        [HarmonyPrefix]
        public static bool CharPrefix()
        {
            return ModInformation.IsServer;
        }

        [HarmonyPatch(typeof(PartyVisual), nameof(PartyVisual.AddMountToPartyIcon))]
        [HarmonyPrefix]
        public static bool MountPrefix()
        {
            return ModInformation.IsServer;
        }
    }
}
