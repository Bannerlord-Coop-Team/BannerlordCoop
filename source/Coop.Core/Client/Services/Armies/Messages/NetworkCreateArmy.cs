using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Armies.Messages;

/// <summary>
/// Network message to commmand the deletion of an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCreateArmy : ICommand
{
    [ProtoMember(1)]
    public ArmyCreationData Data { get; }

    public NetworkCreateArmy(ArmyCreationData armyCreationData)
    {
        Data = armyCreationData;
    }
}