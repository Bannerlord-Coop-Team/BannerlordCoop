using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

public readonly struct RulingClanChanged : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly Clan Clan;

    public RulingClanChanged(Kingdom kingdom, Clan clan)
    {
        Kingdom = kingdom;
        Clan = clan;
    }
}
