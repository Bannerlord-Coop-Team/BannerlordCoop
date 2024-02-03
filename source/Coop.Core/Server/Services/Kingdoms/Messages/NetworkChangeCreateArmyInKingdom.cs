using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;

[ProtoContract(SkipConstructor = true)]
public class NetworkChangeCreateArmyInKingdom : IEvent
{
    [ProtoMember(1)]
    public ArmyCreationData Data { get; }

    public NetworkChangeCreateArmyInKingdom(ArmyCreationData armyCreationData)
    {
        Data = armyCreationData;
    }
}