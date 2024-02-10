using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Used to notify client that the GarrisonWagePaymentLimit needs to be changed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkChangeSettlementGarrisonWagePaymentLimit : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int GarrisonWagePaymentLimit { get; }

    public NetworkChangeSettlementGarrisonWagePaymentLimit(string settlementId, int garrisonWagePaymentLimit)
    {
        SettlementId = settlementId;
        GarrisonWagePaymentLimit = garrisonWagePaymentLimit;
    }
}
