using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Audit;


/// <summary>
/// To request an settlement audit
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record RequestSettlementAudit : IAuditRequest
{
    [ProtoMember(1)]
    public SettlementAuditData[] Data { get; }

    IAuditData[] IAuditRequest.Data => Data;

    public RequestSettlementAudit(SettlementAuditData[] data)
    {
        Data = data;
    }
}
