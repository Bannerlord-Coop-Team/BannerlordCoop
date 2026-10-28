using Common.Logging;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Banners.Extensions;

internal static class BannerCodeExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<Banner>();

    /// <summary>
    /// Whether a banner code parses to at least one layer. A code this side can't read (corrupt, or
    /// using a format it doesn't know) gives a banner with no layers, which throws on the next colour change.
    /// TryGetBannerDataFromCode alone isn't enough, because it returns true for a code too short to hold a layer.
    /// </summary>
    public static bool HasBannerLayers(this string bannerCode) =>
        !string.IsNullOrEmpty(bannerCode) &&
        Banner.TryGetBannerDataFromCode(bannerCode, out var layers) &&
        layers.Count > 0;

    /// <summary>
    /// Builds a banner from a received code. No code stays no banner, and a code without readable layers
    /// gets vanilla's default banner seeded by the code, so every receiver builds the same one.
    /// </summary>
    public static Banner ToBannerWithLayers(this string bannerCode)
    {
        if (bannerCode == null) return null;
        if (bannerCode.HasBannerLayers()) return new Banner(bannerCode);

        Logger.Warning("Received a banner code with no readable layers ({BannerCode}), using a default banner", bannerCode);
        // A seed of -1 means unseeded, so keep it positive.
        return Banner.CreateRandomClanBanner(bannerCode.GetDeterministicHashCode() & int.MaxValue);
    }
}
