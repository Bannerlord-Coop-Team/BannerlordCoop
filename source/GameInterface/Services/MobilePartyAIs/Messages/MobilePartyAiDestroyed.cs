using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Messages;
internal class MobilePartyAiDestroyed : IEvent
{
    public MobilePartyAiDestroyed(MobilePartyAi instance)
    {
        Instance = instance;
    }

    public MobilePartyAi Instance { get; }
}
