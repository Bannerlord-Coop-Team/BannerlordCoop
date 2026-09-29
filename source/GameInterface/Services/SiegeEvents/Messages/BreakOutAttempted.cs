using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Messages;

public readonly struct BreakOutAttempted : IEvent
{
    public MobileParty Party { get; }
    public BreakOutAttempted(MobileParty party) => Party = party;
}
