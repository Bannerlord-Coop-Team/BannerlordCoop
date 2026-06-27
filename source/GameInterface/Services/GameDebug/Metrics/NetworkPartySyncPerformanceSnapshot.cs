using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.GameDebug.Metrics;

[ProtoContract(SkipConstructor = true)]
public record NetworkPartySyncPerformanceSnapshot : IEvent
{
    [ProtoMember(1)]
    public int RequestId { get; }

    [ProtoMember(2)]
    public PartySyncPerformanceData[] Data { get; }

    public NetworkPartySyncPerformanceSnapshot(int requestId, PartySyncPerformanceData[] data)
    {
        RequestId = requestId;
        Data = data;
    }
}
