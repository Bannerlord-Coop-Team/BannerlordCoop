using Common.Logging;
using Common.Util;
using GameInterface.Services.Banners.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(Clan), nameof(Clan.PreAfterLoad))]
internal class ClanBannerLoadRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanBannerLoadRepairPatch>();

    // PreAfterLoad repaints the clan banner next, which is the kingdom banner for a ruling clan, and throws
    // on one with no layers. The replacement is vanilla's default for a clan or kingdom without a banner,
    // seeded by the StringId so every peer loading the save gets the same one.
    [HarmonyPrefix]
    private static void RepairEmptyBanners(Clan __instance)
    {
        var banner = __instance._banner;
        if (banner != null && banner.IsBannerDataListEmpty())
        {
            using (new AllowedThread())
            {
                __instance._banner = __instance.StringId.ToSeededDefaultBanner();
            }

            Logger.Warning("Gave clan {ClanId} a default banner because its saved banner had no layers", __instance.StringId);
        }

        // Vanilla always gives a kingdom a banner, and Coop copies it without a null check.
        var kingdom = __instance.Kingdom;
        if (kingdom != null && (kingdom.Banner == null || kingdom.Banner.IsBannerDataListEmpty()))
        {
            using (new AllowedThread())
            {
                kingdom.Banner = kingdom.StringId.ToSeededDefaultBanner();
            }

            Logger.Warning("Gave kingdom {KingdomId} a default banner because its saved banner was missing or had no layers", kingdom.StringId);
        }
    }
}
