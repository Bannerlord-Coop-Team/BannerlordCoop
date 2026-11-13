using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players.Messages;

public readonly struct PlayerPartyRestoredOnReconnect : IEvent
{
    public readonly MobileParty PlayerParty;

    public PlayerPartyRestoredOnReconnect(MobileParty playerParty)
    {
        PlayerParty = playerParty;
    }
}
