using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Peer → mission host: claim a siege machine's simulation (our player manned it), or release it
/// back (nothing of ours has used it for a while). The host arbitrates and answers with
/// <see cref="NetworkSiegeMachineAuthority"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeMachineClaim : IEvent
{
    [ProtoMember(1)]
    public readonly int MachineId;
    [ProtoMember(2)]
    public readonly string ControllerId;
    [ProtoMember(3)]
    public readonly bool IsRelease;

    public NetworkSiegeMachineClaim(int machineId, string controllerId, bool isRelease)
    {
        MachineId = machineId;
        ControllerId = controllerId;
        IsRelease = isRelease;
    }
}
