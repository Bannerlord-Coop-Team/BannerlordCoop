using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>
/// Server to clients: the donor's freed initial-spawn slot now belongs to a player that joined a full battle.
/// Reserve refreshes carrying the transfer are sent before this assignment on the reliable-ordered stream.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePrioritySlotAssigned : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly long TransferId;
    [ProtoMember(3)]
    public readonly string WaitingPartyId;
    [ProtoMember(4)]
    public readonly string DonorPartyId;

    public NetworkBattlePrioritySlotAssigned(
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
