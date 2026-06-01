using Common.Messaging;
using GameInterface.Services.Banners.Messages;
using HarmonyLib;
using SandBox.GauntletUI.BannerEditor;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Banners.Patches
{
    /// <summary>
    /// Detects when the local player finishes editing their banner and publishes a
    /// <see cref="PlayerBannerChanged"/> event so the change can be propagated across the network.
    /// </summary>
    [HarmonyPatch(typeof(GauntletBannerEditorScreen), nameof(GauntletBannerEditorScreen.OnDone))]
    internal class BannerEditorDonePatch
    {
        // Postfix: the original OnDone has already applied the edited banner and colors to the clan locally.
        static void Postfix(GauntletBannerEditorScreen __instance)
        {
            Clan clan = __instance._clan;

            if (clan?.Banner == null) return;

            // Pass the clan through; the handler resolves its network id via the object manager.
            MessageBroker.Instance.Publish(__instance, new PlayerBannerChanged(clan));
        }
    }
}
