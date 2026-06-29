using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.GameDebug.Metrics;

[ProtoContract(SkipConstructor = true)]
public record NetworkRequestPartySyncPerformanceSnapshot : ICommand
{
    [ProtoMember(1)]
    public int RequestId { get; }

    public NetworkRequestPartySyncPerformanceSnapshot(int requestId)
    {
        RequestId = requestId;
    }
}
