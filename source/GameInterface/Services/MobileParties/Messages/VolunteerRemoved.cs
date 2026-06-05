using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct VolunteerRemoved : IEvent
{
    public readonly Hero Individual;
    public readonly int BitCode;

    public VolunteerRemoved(Hero individual, int bitCode)
    {
        Individual = individual;
        BitCode = bitCode;
    }
}