using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Mission host to peers: the host reset a siege machine to undamaged (a scripted repair), so peers run
/// the same native reset before the next machine state applies.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeMachineReset : IEvent
{
    [ProtoMember(1)]
    public readonly int MachineId;
    /// <summary>BR-102: the sender's host epoch; a deposed host's reset is dropped.</summary>
    [ProtoMember(2)]
    public readonly int HostEpoch;
    [ProtoMember(3)]
    public readonly string SenderControllerId;

    public NetworkSiegeMachineReset(int machineId, int hostEpoch, string senderControllerId)
    {
        MachineId = machineId;
        HostEpoch = hostEpoch;
        SenderControllerId = senderControllerId;
    }
}
