using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>Battle host to peers over the mission mesh, every second: the water time its waves read, which every client adopts.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleWaterTime : IEvent
{
    /// <summary>The host's written water time when sent, in the engine's water clock units.</summary>
    [ProtoMember(1)] public readonly float WaterTime;
    /// <summary>The sender's battle host epoch; a superseded host's time is dropped.</summary>
    [ProtoMember(2)] public readonly int HostEpoch;

    public NetworkBattleWaterTime(float waterTime, int hostEpoch)
    {
        WaterTime = waterTime;
        HostEpoch = hostEpoch;
    }
}
