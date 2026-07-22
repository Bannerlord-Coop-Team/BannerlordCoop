using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Server to clients: a connected player is again waiting for a priority human-agent slot.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattlePriorityWaitQueued : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string WaitingPartyId;
    [ProtoMember(3)]
    public readonly bool ResetExistingState;

    public NetworkBattlePriorityWaitQueued(
        string mapEventId,
        string waitingPartyId,
        bool resetExistingState)
    {
        MapEventId = mapEventId;
        WaitingPartyId = waitingPartyId;
        ResetExistingState = resetExistingState;
    }
}
