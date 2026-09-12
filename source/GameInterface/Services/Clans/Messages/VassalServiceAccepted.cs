using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct VassalServiceAccepted : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly bool GrantRewards;

    public VassalServiceAccepted(Kingdom kingdom, bool grantRewards)
    {
        Kingdom = kingdom;
        GrantRewards = grantRewards;
    }
}
