using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
///  Notify clients to Change Settlement.LastVistTimeOfOwner.
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkChangeLastVisitTimeOfOwner : IEvent
{
    [ProtoMember(1)]
    public string SettlementID { get; }
    [ProtoMember(2)]
    public float CurrentTime { get; }

    public NetworkChangeLastVisitTimeOfOwner(string settlementID, float currentTime)
    {
        SettlementID = settlementID;
        CurrentTime = currentTime;
    }
}
