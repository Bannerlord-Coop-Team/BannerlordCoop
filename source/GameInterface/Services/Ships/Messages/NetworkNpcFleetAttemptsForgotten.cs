using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// Server to every client: the naval battle's mission instance emptied, so its recorded AI fleet attempts are gone and a mission
/// that re-engages the battle attempts its fleets anew.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNpcFleetAttemptsForgotten : IEvent
{
    [ProtoMember(1)] public readonly string InstanceId;

    public NetworkNpcFleetAttemptsForgotten(string instanceId)
    {
        InstanceId = instanceId;
    }
}
