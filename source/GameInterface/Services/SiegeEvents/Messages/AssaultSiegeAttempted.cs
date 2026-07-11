using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The besieger leader chose to assault the walls; ask the server to start the assault.
/// </summary>
public readonly struct AssaultSiegeAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public AssaultSiegeAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
