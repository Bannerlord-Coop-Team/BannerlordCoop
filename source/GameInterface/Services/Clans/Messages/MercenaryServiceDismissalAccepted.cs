using Common.Messaging;
using GameInterface.Serialization.External;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct MercenaryServiceDismissalAccepted : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly Clan Clan;
    public MercenaryServiceDismissalAccepted(Kingdom kingdom, Clan clan)
    {
        Kingdom = kingdom;
        Clan = clan;
    }
}
