using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Mission host to peers: a battering ram struck (the wind-up/hit swing). The peer plays the ram body animation
/// for the given power stage, mirroring BatteringRam.StartHitAnimationWithProgress — its own ram is unmanned and
/// dormant, so nothing plays otherwise. The crew's pull animation rides the normal agent animation sync.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkRamHit : IEvent
{
    [ProtoMember(1)]
    public int MachineId { get; }
    [ProtoMember(2)]
    public int PowerStage { get; }
    [ProtoMember(3)]
    public float Progress { get; }

    public NetworkRamHit(int machineId, int powerStage, float progress)
    {
        MachineId = machineId;
        PowerStage = powerStage;
        Progress = progress;
    }
}
