using HarmonyLib;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(GameStateManager))]
    internal class GameUIDisable
    {
        [HarmonyPatch("PushState")]
        [HarmonyPrefix]
        public static bool PushStatePatch(TaleWorlds.Core.GameState gameState)
        {
            return gameState switch
            {
                ClanState => false,
                KingdomState => false,
                QuestsState => false,
                CharacterDeveloperState => false,
                PartyState => false,
                InventoryState => false,
                _ => true,
            };
        }
    }
}
