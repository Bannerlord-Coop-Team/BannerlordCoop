using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// Response message for the settlement audit.
/// </summary>
/// 
[ProtoContract(SkipConstructor = true)]
public record SettlementAuditResponse : IAuditResponse<SettlementAuditData>
{
    [ProtoMember(1)]
    public SettlementAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    public SettlementAuditResponse(SettlementAuditData[] data, string serverAuditResults)
    {
        Data = data;
        ServerAuditResult = serverAuditResults;
    }
}
