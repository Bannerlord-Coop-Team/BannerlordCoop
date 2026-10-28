using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(Clan), nameof(Clan.PreAfterLoad))]
internal class ClanBannerLoadRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanBannerLoadRepairPatch>();

    [HarmonyPrefix]
    private static void RepairEmptyBanner(Clan __instance)
    {
        var banner = __instance._banner;
        if (banner == null || !banner.IsBannerDataListEmpty()) return;

        // PreAfterLoad repaints the banner next and throws on one with no layers. This is vanilla's default
        // for a clan without a banner, seeded by the clan id so every peer loading the save gets the same one.
        using (new AllowedThread())
        {
            __instance._banner = Banner.CreateRandomClanBanner(__instance.StringId.GetDeterministicHashCode());
        }

        Logger.Warning("Gave clan {ClanId} a default banner because its saved banner had no layers", __instance.StringId);
    }
}
