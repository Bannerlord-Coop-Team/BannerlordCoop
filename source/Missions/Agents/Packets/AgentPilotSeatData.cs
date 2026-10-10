using ProtoBuf;

namespace Missions.Agents.Packets;

[ProtoContract]
public readonly struct AgentPilotSeatData
{
    [ProtoMember(1)] public string BattleId { get; }
    [ProtoMember(2)] public int MachineId { get; }
    [ProtoMember(3)] public int PointId { get; }
    [ProtoMember(4)] public int HostEpoch { get; }
    [ProtoMember(5)] public int MachineRevision { get; }
    [ProtoMember(6)] public long AgentRevision { get; }
    [ProtoMember(7)] public bool Using { get; }

    public AgentPilotSeatData(string battleId, int machineId, int pointId, int hostEpoch,
        int machineRevision, long agentRevision, bool usingSeat)
    {
        BattleId = battleId;
        MachineId = machineId;
        PointId = pointId;
        HostEpoch = hostEpoch;
        MachineRevision = machineRevision;
        AgentRevision = agentRevision;
        Using = usingSeat;
    }

    internal AgentPilotSeatData Stopped(long agentRevision) =>
        new(BattleId, MachineId, PointId, HostEpoch, MachineRevision, agentRevision, false);
}
