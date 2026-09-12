using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player chose to join an ongoing siege camp; ask the server to add the party.
/// </summary>
public readonly struct JoinSiegeCampAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public JoinSiegeCampAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
