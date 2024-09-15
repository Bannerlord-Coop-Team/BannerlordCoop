using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Messages;
internal class MobilePartyAiCreated : IEvent
{
    public MobilePartyAiCreated(MobilePartyAi instance, MobileParty party)
    {
        Instance = instance;
        Party = party;
    }

    public MobilePartyAi Instance { get; }
    public MobileParty Party { get; }
}
