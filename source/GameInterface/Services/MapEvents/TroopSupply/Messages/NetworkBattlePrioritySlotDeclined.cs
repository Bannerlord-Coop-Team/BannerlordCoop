using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Waiting player to server: its assigned hero cannot spawn, so the slot must be reassigned or restored.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePrioritySlotDeclined : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly long TransferId;
    [ProtoMember(3)]
    public readonly string WaitingPartyId;

    public NetworkBattlePrioritySlotDeclined(string mapEventId, long transferId, string waitingPartyId)
    {
        MapEventId = mapEventId;
        TransferId = transferId;
        WaitingPartyId = waitingPartyId;
    }
}
