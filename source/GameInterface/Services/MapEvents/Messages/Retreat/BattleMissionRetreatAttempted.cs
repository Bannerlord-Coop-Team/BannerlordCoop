using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Retreat;

/// <summary>
/// The local player left a battle MISSION without the battle resolving - the scoreboard's retreat, or any
/// other exit that ends the mission with no result. Ask the server to take the party out of the battle.
/// </summary>
/// <remarks>
/// Distinct from <see cref="BattleRetreatAttempted"/>, which is the campaign menu's "Try to get away." and
/// carries vanilla's get-away cost. This one is free: the price of leaving an assault is the casualties
/// already taken in it, and vanilla charges nothing further.
///
/// Only published when the mission produced no resolved result. A mission that ended in a victory or a
/// defeat concludes through the completion barrier instead, which is the path that forfeits rosters and
/// captures the losers - a retreat must never reach it.
/// </remarks>
public readonly struct BattleMissionRetreatAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly MapEvent Battle;

    public BattleMissionRetreatAttempted(MobileParty party, MapEvent battle)
    {
        Party = party;
        Battle = battle;
    }
}
