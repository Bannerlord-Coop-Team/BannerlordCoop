using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct SetClanKingdom : IEvent
{
    public readonly Clan Clan;
    public readonly Kingdom Kingdom;

    public SetClanKingdom(Clan clan, Kingdom kingdom)
    {
        Clan = clan;
        Kingdom = kingdom;
    }
}
