using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SettlementComponents.Audit;

/// <summary>
/// Response message for the settlementcomponent audit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record SettlementComponentAuditResponse :IEvent
{
    [ProtoMember(1)]
    public SettlementComponentAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    public SettlementComponentAuditResponse(SettlementComponentAuditData[] data, string serverAuditResult)
    {
        Data = data;
        ServerAuditResult = serverAuditResult;
    }
}


