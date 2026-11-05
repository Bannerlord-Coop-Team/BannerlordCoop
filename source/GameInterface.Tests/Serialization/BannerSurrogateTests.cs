using GameInterface.Surrogates;
using ProtoBuf;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class BannerSurrogateTests
{
    private const string BannerCode = "11.4.4.1528.1528.764.764.1.0.0.133.5.5.512.512.764.764.0.0.0";

    public BannerSurrogateTests()
    {
        // The default banner takes its colours from the palette, which is empty outside the game.
        BannerManager.Initialize();
        if (BannerManager.Instance.ReadOnlyColorPalette.Count == 0)
        {
            BannerManager.Instance._colorPalette[9101] = new BannerColor(0xFF102030, true, true);
            BannerManager.Instance.ReadOnlyColorPalette = BannerManager.Instance._colorPalette.GetReadOnlyDictionary();
        }
    }

    [Fact]
    public void RoundTrip_KeepsReadableBanner()
    {
        Assert.Equal(BannerCode, RoundTrip(new Banner(BannerCode)).Serialize());
    }

    [Fact]
    public void RoundTrip_NoBanner_StaysNoBanner()
    {
        Assert.Null(RoundTrip(null));
    }

    [Fact]
    public void RoundTrip_EmptyBanner_StaysEmptyBanner()
    {
        // A weapon without a banner is sent with an empty one.
        Banner roundTripped = RoundTrip(new Banner());

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.IsBannerDataListEmpty());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FromEmptyCode_GivesEmptyBanner(string bannerCode)
    {
        Banner banner = RoundTripCode(bannerCode);

        Assert.NotNull(banner);
        Assert.True(banner.IsBannerDataListEmpty());
    }

    [Fact]
    public void RoundTrip_CodeWithoutLayers_GivesDefaultBanner()
    {
        Banner banner = RoundTripCode("11.9001");

        Assert.False(banner.IsBannerDataListEmpty());
    }

    [Theory]
    [InlineData("11.9001")]
    [InlineData("11.9001.9001.1528.1528.764.764.1.0.x")]
    public void FromCodeWithoutLayers_GivesSameDefaultBannerEveryTime(string bannerCode)
    {
        Banner first = new BannerSurrogate { Data = bannerCode };
        Banner second = new BannerSurrogate { Data = bannerCode };

        Assert.False(first.IsBannerDataListEmpty());
        Assert.Equal(first.Serialize(), second.Serialize());
    }

    private static Banner RoundTrip(Banner banner) => RoundTripSurrogate(banner);

    private static Banner RoundTripCode(string bannerCode) => RoundTripSurrogate(new BannerSurrogate { Data = bannerCode });

    private static Banner RoundTripSurrogate(BannerSurrogate surrogate)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, surrogate);
        stream.Position = 0;
        return Serializer.Deserialize<BannerSurrogate>(stream);
    }
}
