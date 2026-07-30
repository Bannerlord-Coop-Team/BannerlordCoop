using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct MercenaryServiceAccepted : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly int AwardMultiplier;
    public readonly Clan Clan;

    public MercenaryServiceAccepted(Kingdom kingdom, int awardMultiplier, Clan clan)
    {
        Kingdom = kingdom;
        AwardMultiplier = awardMultiplier;
        Clan = clan;
    }
}
