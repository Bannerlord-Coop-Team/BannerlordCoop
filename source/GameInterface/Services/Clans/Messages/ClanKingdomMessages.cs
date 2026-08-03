using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct ClanEntersKingdom : IEvent
{
    public readonly Clan Clan;

    public ClanEntersKingdom(Clan clan)
    {
        Clan = clan;
    }
}

public readonly struct ClanLeavesKingdom : IEvent
{
    public readonly Clan Clan;

    public ClanLeavesKingdom(Clan clan)
    {
        Clan = clan;
    }
}

public readonly struct UpdateBannerColorsOfClan : IEvent
{
    public readonly Clan Clan;

    public UpdateBannerColorsOfClan(Clan clan)
    {
        Clan = clan;
    }
}
