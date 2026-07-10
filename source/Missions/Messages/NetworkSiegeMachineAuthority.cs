using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Mission host → peers: who simulates a siege machine. Broadcast on every claim change and
/// replayed to joiners; appliers are idempotent.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeMachineAuthority : IEvent
{
    [ProtoMember(1)]
    public readonly int MachineId;
    /// <summary>Owning controller; empty hands the machine back to the mission host.</summary>
    [ProtoMember(2)]
    public readonly string ControllerId;

    public NetworkSiegeMachineAuthority(int machineId, string controllerId)
    {
        MachineId = machineId;
        ControllerId = controllerId;
    }
}
