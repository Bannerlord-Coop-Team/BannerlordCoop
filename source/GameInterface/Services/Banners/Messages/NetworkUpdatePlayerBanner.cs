using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Banners.Messages
{
    /// <summary>
    /// Network command instructing all peers to apply a clan's edited banner.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkUpdatePlayerBanner : ICommand
    {
        [ProtoMember(1)]
        public string BannerCode { get; }

        [ProtoMember(2)]
        public string ClanId { get; }

        [ProtoMember(3)]
        public uint Color { get; }

        [ProtoMember(4)]
        public uint Color2 { get; }

        public NetworkUpdatePlayerBanner(string bannerCode, string clanId, uint color, uint color2)
        {
            BannerCode = bannerCode;
            ClanId = clanId;
            Color = color;
            Color2 = color2;
        }
    }
}
