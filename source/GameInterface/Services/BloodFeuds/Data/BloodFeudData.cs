using ProtoBuf;

namespace GameInterface.Services.BloodFeuds.Data;

/// <summary>
/// The counts stay after a feud is settled by relations, like vanilla
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class BloodFeudData
{
    [ProtoMember(1)]
    public bool HasBloodFeud { get; set; }

    [ProtoMember(2)]
    public int ExecutionsDoneCount { get; set; }

    [ProtoMember(3)]
    public int ExecutionsReceivedCount { get; set; }
}
