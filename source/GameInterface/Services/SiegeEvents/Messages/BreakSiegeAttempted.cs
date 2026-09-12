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
    /// Whether the approval should finish the local encounter and menu. This is false when the
    /// publishing vanilla flow keeps running and owns its own continuation.
    /// </summary>
    public readonly bool FinishLocalMenus;

    public BreakSiegeAttempted(MobileParty party, bool finishLocalMenus = true)
    {
        Party = party;
        FinishLocalMenus = finishLocalMenus;
    }
}
