using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateArmy : ICommand
{
    [ProtoMember(1)]
    public ArmyCreationData Data { get; }

    public NetworkCreateArmy(ArmyCreationData data)
    {
        Data = data;
    }
}
