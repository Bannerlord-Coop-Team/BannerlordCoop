using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct ClanMercenaryServiceChanged : IEvent
{
    public readonly Clan Clan;
    public readonly bool IsUnderMercenaryService;

    public ClanMercenaryServiceChanged(
        Clan clan,
        bool isUnderMercenaryService)
    {
        Clan = clan;
        IsUnderMercenaryService = isUnderMercenaryService;
    }
}
