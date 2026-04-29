using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.Core;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch]
internal class GameLoadedPatch
{
    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.OnInitialize))]
    [HarmonyPostfix]
    static void OnGameLoaded()
    {
        // Removes disabled states fixing camera bug when new game is loaded,
        // I think this is because opening the escape menu is supposed to call this but it never opens here
        Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(MapScreen.Instance);

        MessageBroker.Instance.Publish(null, new CampaignReady());
    }
}
