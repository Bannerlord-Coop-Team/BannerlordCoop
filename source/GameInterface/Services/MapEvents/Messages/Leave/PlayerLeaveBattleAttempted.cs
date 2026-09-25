using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Leave;

// A joiner party leaves a battle without ending it; the server performs the authoritative removal.
public readonly struct PlayerLeaveBattleAttempted : IEvent
{
    public readonly PartyBase LeavingParty;
    public readonly bool FinishLocalMenus;

    public readonly bool BreakSiege;

    public PlayerLeaveBattleAttempted(PartyBase leavingParty, bool finishLocalMenus = true, bool breakSiege = false)
    {
        LeavingParty = leavingParty;
        FinishLocalMenus = finishLocalMenus;
        BreakSiege = breakSiege;
    }
}
