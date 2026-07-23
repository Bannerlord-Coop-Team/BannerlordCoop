using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct MercenaryServiceAccepted : IEvent
{
    public readonly Kingdom Kingdom;

    public MercenaryServiceAccepted(Kingdom kingdom)
    {
        Kingdom = kingdom;
    }
}
