using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Server to clients: a consumed transfer stopped gating after its priority human departed.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePrioritySlotSettled : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly long TransferId;
    [ProtoMember(3)]
    public readonly string WaitingPartyId;

    public NetworkBattlePrioritySlotSettled(
        string mapEventId,
        long transferId,
        string waitingPartyId)
    {
        MapEventId = mapEventId;
        TransferId = transferId;
        WaitingPartyId = waitingPartyId;
    }
}
