using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>
/// Server to clients: an unconsumed priority transfer was cancelled because its waiting player departed.
/// The donor's restored reserve scope is sent first on the reliable-ordered stream.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePrioritySlotCancelled : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly long TransferId;
    [ProtoMember(3)]
    public readonly string WaitingPartyId;
    [ProtoMember(4)]
    public readonly string DonorPartyId;

    public NetworkBattlePrioritySlotCancelled(
        string mapEventId,
        long transferId,
        string waitingPartyId,
        string donorPartyId)
    {
        MapEventId = mapEventId;
        TransferId = transferId;
        WaitingPartyId = waitingPartyId;
        DonorPartyId = donorPartyId;
    }
}
