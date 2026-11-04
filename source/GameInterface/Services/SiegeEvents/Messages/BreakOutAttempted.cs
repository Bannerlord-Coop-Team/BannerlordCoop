using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Messages;

public readonly struct BreakOutAttempted : IEvent
{
    public MobileParty Party { get; }
    public bool IsFromPort { get; }

    public BreakOutAttempted(MobileParty party, bool isFromPort)
    {
        Party = party;
        IsFromPort = isFromPort;
    }
}
