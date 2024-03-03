using GameInterface.Services.UI.Handlers;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Patches
{

    [HarmonyPatch(typeof(LoadingWindow))]
    internal class ClientGameMenuPatch
    {
        public static bool IsDoneLoading = false;
        [HarmonyPatch("DisableGlobalLoadingWindow")]
        [HarmonyPrefix]
        public static bool PushStatePatch()
        {
            if (ModInformation.IsServer) return true;

            if(IsDoneLoading) return true;
            return false;
        }
    }
}
