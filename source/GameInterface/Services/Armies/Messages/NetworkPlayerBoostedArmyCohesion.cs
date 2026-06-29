using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to boost army cohesion
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerBoostedArmyCohesion : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyLeaderPartyId;
    [ProtoMember(2)]
    public readonly float CohesionToGain;
    [ProtoMember(3)]
    public readonly int InfluenceCost;

    public NetworkPlayerBoostedArmyCohesion(string armyLeaderPartyId, float cohesionToGain, int influenceCost)
    {
        ArmyLeaderPartyId = armyLeaderPartyId;
        CohesionToGain = cohesionToGain;
        InfluenceCost = influenceCost;
    }
}