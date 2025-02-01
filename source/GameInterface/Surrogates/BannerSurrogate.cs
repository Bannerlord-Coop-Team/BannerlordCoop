using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
        return new Banner(surrogate.Data);
    }
}