using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Waiting player to server: its assigned priority slot successfully spawned and cannot be rolled back.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePrioritySlotConsumed : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly long TransferId;
    [ProtoMember(3)]
    public readonly string WaitingPartyId;

    public NetworkBattlePrioritySlotConsumed(string mapEventId, long transferId, string waitingPartyId)
    {
        MapEventId = mapEventId;
        TransferId = transferId;
        WaitingPartyId = waitingPartyId;
    }
}
