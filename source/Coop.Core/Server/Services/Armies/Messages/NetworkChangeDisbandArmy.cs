using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Armies.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkChangeDisbandArmy : IEvent
{
    [ProtoMember(1)]
    public ArmyDeletionData Data { get; }

    public NetworkChangeDisbandArmy(ArmyDeletionData armyDeletionData)
    {
        Data = armyDeletionData;
    }
}