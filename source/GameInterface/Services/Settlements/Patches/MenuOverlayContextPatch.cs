using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(GameMenuOverlayFactory))]
    public static class GameMenuOverlayFactoryContextPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("GetOverlay")]
        public static void Prefix()
        {
            var enc = PlayerEncounter.EncounterSettlement;
            if (enc != null)
            {
                // Contexte Settlement déjà géré par l'engine via PlayerEncounter
            }
        }
    }
}
