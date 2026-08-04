using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Retreat;

/// <summary>
/// The local player chose "Try to get away." out of a battle; ask the server to apply the cost.
/// </summary>
/// <remarks>
/// Carries the acting party explicitly so nothing downstream re-reads MobileParty.MainParty - the
/// server has none, and on a client it would silently mean "whoever is local" rather than the requester.
/// </remarks>
public readonly struct BattleRetreatAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly MapEvent Battle;

    public BattleRetreatAttempted(MobileParty party, MapEvent battle)
    {
        Party = party;
        Battle = battle;
    }
}
