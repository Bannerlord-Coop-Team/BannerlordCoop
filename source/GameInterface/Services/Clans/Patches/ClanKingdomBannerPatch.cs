using Common;
using Common.Messaging;
using GameInterface.Services.Banners.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ChangeKingdomAction), "ApplyInternal")]
internal class ClanKingdomBannerPatch
{
    [HarmonyPostfix]
    private static void Postfix(Clan clan)
    {
        if (!ModInformation.IsServer || clan?.Banner == null) return;

        // Reuse the established banner update path, which also invalidates client map visuals.
        MessageBroker.Instance.Publish(clan, new PlayerBannerChanged(clan));
    }
}
