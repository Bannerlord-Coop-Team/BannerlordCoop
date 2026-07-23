using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify client of lastthreat time change..
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementLastThreatTime : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public long? LastThreatTimeTicks { get; }

    public NetworkChangeSettlementLastThreatTime(string settlementId, long? lastThreatTimeTicks)
    {
        SettlementId = settlementId;
        LastThreatTimeTicks = lastThreatTimeTicks;
    }
}
