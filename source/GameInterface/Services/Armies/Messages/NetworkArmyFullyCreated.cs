using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArmyFullyCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;

    public NetworkArmyFullyCreated(string armyId)
    {
        ArmyId = armyId;
    }
}
