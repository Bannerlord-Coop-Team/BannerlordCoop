using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct UpdateBannerColorsOfClan : IEvent
{
    public readonly Clan Clan;

    public UpdateBannerColorsOfClan(Clan clan)
    {
        Clan = clan;
    }
}
