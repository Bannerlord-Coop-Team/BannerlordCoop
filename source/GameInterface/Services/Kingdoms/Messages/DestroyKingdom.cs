using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

public readonly struct DestroyKingdom : IEvent
{
    public readonly Kingdom Kingdom;

    public DestroyKingdom(Kingdom kingdom)
    {
        Kingdom = kingdom;
    }
}
