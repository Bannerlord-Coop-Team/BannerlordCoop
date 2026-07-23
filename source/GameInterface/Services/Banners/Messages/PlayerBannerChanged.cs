using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Banners.Messages
{
    /// <summary>
    /// Local event raised when the player finishes editing their banner from the game interface.
    /// The <see cref="Clan"/> is passed through so the handler can resolve its network id via the
    /// object manager (resolving from a StringId would fail for newly created clans).
    /// </summary>
    public record PlayerBannerChanged : IEvent
    {
        public Clan Clan { get; }

        public PlayerBannerChanged(Clan clan)
        {
            Clan = clan;
        }
    }
}
