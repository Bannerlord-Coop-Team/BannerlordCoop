using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a player boosts army cohesion via the army management UI
/// </summary>
public readonly struct PlayerBoostedArmyCohesion : IEvent
{
    public readonly MobileParty ArmyLeaderParty;
    public readonly float CohesionToGain;
    public readonly int InfluenceCost;

    public PlayerBoostedArmyCohesion(MobileParty armyLeaderParty, float cohesionToGain, int influenceCost)
    {
        ArmyLeaderParty = armyLeaderParty;
        CohesionToGain = cohesionToGain;
        InfluenceCost = influenceCost;
    }
}