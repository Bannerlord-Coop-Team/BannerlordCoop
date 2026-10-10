using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

public enum MangonelPickupPhase { Request, Granted, Denied }

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkMangonelAmmoPickup : IEvent
{
    [ProtoMember(1)] public string BattleId { get; }
    [ProtoMember(2)] public Guid RequestId { get; }
    [ProtoMember(3)] public Guid AgentId { get; }
    [ProtoMember(4)] public string AgentOwner { get; }
    [ProtoMember(5)] public long AgentRevision { get; }
    [ProtoMember(6)] public long GrantRevision { get; }
    [ProtoMember(7)] public int MachineId { get; }
    [ProtoMember(8)] public int PointId { get; }
    [ProtoMember(9)] public string HostControllerId { get; }
    [ProtoMember(10)] public int HostEpoch { get; }
    [ProtoMember(11)] public MangonelPickupPhase Phase { get; }
    [ProtoMember(12)] public int RemainingAmmo { get; }

    public NetworkMangonelAmmoPickup(string battleId, Guid requestId, Guid agentId, string agentOwner,
        long agentRevision, long grantRevision, int machineId, int pointId, string hostControllerId,
        int hostEpoch, MangonelPickupPhase phase = MangonelPickupPhase.Request, int remainingAmmo = -1)
    {
        BattleId = battleId;
        RequestId = requestId;
        AgentId = agentId;
        AgentOwner = agentOwner;
        AgentRevision = agentRevision;
        GrantRevision = grantRevision;
        MachineId = machineId;
        PointId = pointId;
        HostControllerId = hostControllerId;
        HostEpoch = hostEpoch;
        Phase = phase;
        RemainingAmmo = remainingAmmo;
    }

    public NetworkMangonelAmmoPickup Decide(bool granted, int remainingAmmo) =>
        new(BattleId, RequestId, AgentId, AgentOwner, AgentRevision, GrantRevision, MachineId, PointId,
            HostControllerId, HostEpoch, granted ? MangonelPickupPhase.Granted : MangonelPickupPhase.Denied, remainingAmmo);
}
