using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct MercenaryServiceDismissalAccepted : IEvent
{
    public readonly Kingdom Kingdom;

    public MercenaryServiceDismissalAccepted(Kingdom kingdom)
    {
        Kingdom = kingdom;
    }
}
