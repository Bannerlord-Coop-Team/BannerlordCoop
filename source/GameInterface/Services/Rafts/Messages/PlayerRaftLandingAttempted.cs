using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Rafts.Messages;

/// <summary>Published on the server game thread to land a player party stranded on land as a raft or marked at sea.</summary>
public readonly struct PlayerRaftLandingAttempted : IEvent
{
    public readonly MobileParty PlayerParty;

    public PlayerRaftLandingAttempted(MobileParty playerParty)
    {
        PlayerParty = playerParty;
    }
}
