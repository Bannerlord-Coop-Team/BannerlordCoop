using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify client of Militia Change.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementMilitia : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public float Militia { get; }

    public NetworkChangeSettlementMilitia(string settlementId, float militia)
    {
        SettlementId = settlementId;
        Militia = militia;
    }
}
