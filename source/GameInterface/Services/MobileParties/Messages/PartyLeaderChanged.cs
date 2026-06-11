using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// [Server, local] Raised when a party's leader changes, so the change can be replicated to clients.
/// </summary>
internal readonly struct PartyLeaderChanged : IEvent
{
    public readonly MobileParty Party;
    public readonly Hero NewLeader;

    public PartyLeaderChanged(MobileParty party, Hero newLeader)
    {
        Party = party;
        NewLeader = newLeader;
    }
}
