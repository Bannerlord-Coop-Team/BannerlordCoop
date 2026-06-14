using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Instances.Messages;

/// <summary>
/// Server -> client reply to <see cref="NetworkEnterLocation"/>. Carries the server-issued
/// instance id the client should NAT-punch with, and whether this client is the instance host
/// (the peer that owns NPC simulation inside the scene).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAssignInstance : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly string SettlementId;
    [ProtoMember(3)]
    public readonly string LocationId;
    [ProtoMember(4)]
    public readonly bool IsHost;

    public NetworkAssignInstance(string instanceId, string settlementId, string locationId, bool isHost)
    {
        InstanceId = instanceId;
        SettlementId = settlementId;
        LocationId = locationId;
        IsHost = isHost;
    }
}
