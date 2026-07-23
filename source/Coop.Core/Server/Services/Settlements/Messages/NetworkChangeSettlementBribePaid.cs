using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Send the client information of BribePaid change
/// </summary>

[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementBribePaid : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int BribePaid { get; }

    public NetworkChangeSettlementBribePaid(string settlementId, int bribePaid)
    {
        SettlementId = settlementId;
        BribePaid = bribePaid;
    }
}
