using Common;
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
                KingdomState => true,
                QuestsState => false,
                CharacterDeveloperState => ModInformation.IsClient,
                PartyState => ModInformation.IsClient,
                InventoryState => ModInformation.IsClient,
                ClanState => ModInformation.IsClient,
                _ => true,
            };
        }
    }
}
