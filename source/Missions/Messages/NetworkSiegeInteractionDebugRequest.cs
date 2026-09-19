#if DEBUG
using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkSiegeInteractionDebugRequest : ICommand
{
    [ProtoMember(1)] public string MapEventId { get; }
    [ProtoMember(2)] public string ControllerId { get; }
    [ProtoMember(3)] public string RequestId { get; }
    [ProtoMember(4)] public int MachineId { get; }

    [ProtoMember(5)] public string Action { get; }
    [ProtoMember(6)] public int StandingPointIndex { get; }

    public NetworkSiegeInteractionDebugRequest(string mapEventId, string controllerId,
        string requestId, int machineId, string action, int standingPointIndex)
    {
        MapEventId = mapEventId;
        ControllerId = controllerId;
        RequestId = requestId;
        MachineId = machineId;
        Action = action;
        StandingPointIndex = standingPointIndex;
    }
}
#endif
