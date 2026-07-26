using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct MercenaryServiceAccepted : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly int AwardMultiplier;

    public MercenaryServiceAccepted(Kingdom kingdom, int awardMultiplier)
    {
        Kingdom = kingdom;
        AwardMultiplier = awardMultiplier;
    }
}
