using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player chose to leave their siege camp; ask the server to remove the party.
/// </summary>
public readonly struct BreakSiegeAttempted : IEvent
{
    public readonly MobileParty Party;

    public BreakSiegeAttempted(MobileParty party)
    {
        Party = party;
    }
}
