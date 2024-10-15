using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Audit;


/// <summary>
/// To request an settlement audit
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record RequestSettlementAudit : IAuditRequest<SettlementAuditData>
{
    [ProtoMember(1)]
    public SettlementAuditData[] Data { get; }

    public RequestSettlementAudit(SettlementAuditData[] data)
    {
        Data = data;
    }
}
