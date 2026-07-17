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
    /// <summary>
    /// BR-102: the arbitrating host's epoch for this battle. Receivers drop an authority decision
    /// stamped by an earlier hosting generation (a deposed host still arbitrating in flight across a
    /// migration); 0 = unstamped (sender had no assignment yet), always accepted.
    /// </summary>
    [ProtoMember(3)]
    public readonly int HostEpoch;

    public NetworkSiegeMachineAuthority(int machineId, string controllerId, int hostEpoch = 0)
    {
        MachineId = machineId;
        ControllerId = controllerId;
        HostEpoch = hostEpoch;
    }
}
