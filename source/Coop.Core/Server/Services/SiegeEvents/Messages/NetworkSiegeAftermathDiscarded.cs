using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A parked siege aftermath the server dropped without applying, so the leader's client releases its choice hold.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSiegeAftermathDiscarded : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }

    public NetworkSiegeAftermathDiscarded(string settlementId)
    {
        SettlementId = settlementId;
    }
}
