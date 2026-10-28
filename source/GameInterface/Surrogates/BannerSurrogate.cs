using GameInterface.Services.Banners.Extensions;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct BannerSurrogate
{
    [ProtoMember(1)]
    public string Data { get; set; }

    public BannerSurrogate(Banner banner)
    {
        Data = banner?.Serialize();
    }

    public static implicit operator BannerSurrogate(Banner banner)
    {
        return new BannerSurrogate(banner);
    }

    public static implicit operator Banner(BannerSurrogate surrogate)
    {
        // AutoSync sets clan and kingdom banners from this. A kingdom banner can't be null, so a code without
        // readable layers gets a default banner instead of one with no layers.
        return surrogate.Data.ToBannerWithLayers();
    }
}