using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyArmy : ICommand
{
    [ProtoMember(1)]
    public ArmyDestructionData Data { get; }

    public NetworkDestroyArmy(ArmyDestructionData data)
    {
        Data = data;
    }
}
