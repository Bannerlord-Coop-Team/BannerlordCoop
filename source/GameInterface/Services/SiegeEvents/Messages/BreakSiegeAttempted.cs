using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player chose to leave their siege camp; ask the server to remove the party.
/// </summary>
public readonly struct BreakSiegeAttempted : IEvent
{
    public readonly MobileParty Party;

    /// <summary>
    /// True for the leave-menu flows whose native continuation was suppressed: the approval then
    /// finishes the local encounter/menu. False when something else already owns that continuation —
    /// camp writes embedded mid-flow (try-to-get-away, player defeat, safe-passage barter) whose
    /// native flow keeps running, and break_in_leave when a battle leave is routed alongside (its
    /// returning NetworkPartyLeftBattle finishes the encounter) — so the approval must leave the
    /// menus alone.
    /// </summary>
    public readonly bool FinishLocalMenus;

    public BreakSiegeAttempted(MobileParty party, bool finishLocalMenus = true)
    {
        Party = party;
        FinishLocalMenus = finishLocalMenus;
    }
}
