using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Instances.Messages;

/// <summary>
/// Sent by a client when its main party enters an interior location (e.g. a tavern).
/// The server replies with <see cref="NetworkAssignInstance"/> telling the client which P2P
/// instance to join and whether it is the host.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkEnterLocation : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;
    [ProtoMember(2)]
    public readonly string LocationId;

    public NetworkEnterLocation(string settlementId, string locationId)
    {
        SettlementId = settlementId;
        LocationId = locationId;
    }
}
