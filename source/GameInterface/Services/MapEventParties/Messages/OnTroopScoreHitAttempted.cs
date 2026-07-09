using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

/// <summary>
/// A hit scored by one of <see cref="MapEventParty"/>'s troops that the server must account (roster xp,
/// hero combat-hit event and <c>ContributionToBattle</c>). Published on the client that applied the blow:
/// by the <c>MapEventParty.OnTroopScoreHit</c> prefix for native missions, and by <c>CoopAgentOrigin</c>
/// for live coop battles, where the native supplier chain never reaches the map event party.
/// </summary>
public readonly struct OnTroopScoreHitAttempted : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly int TroopSeed;
    public readonly CharacterObject AttackedTroop;
    public readonly int Damage;
    public readonly bool IsFatal;
    public readonly bool IsSimulatedHit;

    public OnTroopScoreHitAttempted(MapEventParty mapEventParty, int troopSeed, CharacterObject attackedTroop, int damage, bool isFatal, bool isSimulatedHit)
    {
        MapEventParty = mapEventParty;
        TroopSeed = troopSeed;
        AttackedTroop = attackedTroop;
        Damage = damage;
        IsFatal = isFatal;
        IsSimulatedHit = isSimulatedHit;
    }
}
