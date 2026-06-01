using Common.Messaging;

namespace GameInterface.Services.Banners.Messages
{
    /// <summary>
    /// Local event raised when the player finishes editing their banner from the game interface.
    /// </summary>
    public record PlayerBannerChanged : IEvent
    {
        public string ClanId { get; }
        public string BannerCode { get; }
        public uint Color { get; }
        public uint Color2 { get; }

        public PlayerBannerChanged(string clanId, string bannerCode, uint color, uint color2)
        {
            ClanId = clanId;
            BannerCode = bannerCode;
            Color = color;
            Color2 = color2;
        }
    }
}
