using Common;
using Common.Messaging;
using GameInterface.Services.GameMenus.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.GameMenus.Patches;

[HarmonyPatch(typeof(GameMenuOverlay))]
internal class GameMenuOverlayPatches
{
    /// <summary>
    /// Certain troop actions use methods that need to be managed by the server
    /// This postfix sends relevant data to run on the server but assumes the original calls are blocked on clients
    /// </summary>
    [HarmonyPatch(nameof(GameMenuOverlay.ExecuteTroopAction))]
    [HarmonyPostfix]
    public static void ExecuteTroopActionPostfix(ref GameMenuOverlay __instance, object o)
    {
        // Shouldn't ever be called on server, but make postfix doesn't run if it is
        if (ModInformation.IsServer) return;

        // Taking hero to party directly when in a settlement where they are assigned
        if ((GameMenuOverlay.MenuOverlayContextList)o == GameMenuOverlay.MenuOverlayContextList.TakeToParty)
        {
            var message = new MenuHeroTakenToParty(__instance._contextMenuItem.Character.HeroObject, MobileParty.MainParty);
            MessageBroker.Instance.Publish(__instance, message);
        }
        
        // Add other MenuOverlayContextList types below if needed
    }
}
