using HarmonyLib;
using Common;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(GameStateManager))]
    internal class GameUIDisable
    {
        [HarmonyPatch("PushState")]
        [HarmonyPrefix]
        public static bool PushStatePatch(TaleWorlds.Core.GameState gameState)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return gameState switch
            {
                KingdomState => false,
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
