using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct AllianceEnded : IEvent
{
    public readonly Kingdom Kingdom1;
    public readonly Kingdom Kingdom2;
    public AllianceEnded(Kingdom kingdom1, Kingdom kingdom2)
    {
        Kingdom1 = kingdom1;
        Kingdom2 = kingdom2;
    }
}
