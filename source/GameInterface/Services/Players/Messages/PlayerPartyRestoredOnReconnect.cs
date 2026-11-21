using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players.Messages;

/// <summary>Published on the server game thread after a reconnected player's parked party is reactivated.</summary>
public readonly struct PlayerPartyRestoredOnReconnect : IEvent
{
    public readonly MobileParty PlayerParty;

    public PlayerPartyRestoredOnReconnect(MobileParty playerParty)
    {
        PlayerParty = playerParty;
    }
}
