using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArmyCohesionChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly float Cohesion;

    public NetworkArmyCohesionChanged(string armyId, float cohesion)
    {
        ArmyId = armyId;
        Cohesion = cohesion;
    }
}
