using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Leave;

// A joiner party leaves a battle without ending it; the server performs the authoritative removal.
public readonly struct PlayerLeaveBattleAttempted : IEvent
{
    public readonly PartyBase LeavingParty;

    public PlayerLeaveBattleAttempted(PartyBase leavingParty)
    {
        LeavingParty = leavingParty;
    }
}
