using HarmonyLib;
using Common;
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
                KingdomState => false,
                QuestsState => false,
                CharacterDeveloperState => ModInformation.IsClient,
                PartyState => false,
                InventoryState => ModInformation.IsClient,
                ClanState => ModInformation.IsClient,
                _ => true,
            };
        }
    }
}
