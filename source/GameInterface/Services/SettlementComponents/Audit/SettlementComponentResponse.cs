using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
namespace GameInterface.Services.SettlementComponents.Audit;

/// <summary>
/// Response message for the settlementcomponent audit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record SettlementComponentAuditResponse : IEvent
{
    [ProtoMember(1)]
    public SettlementComponentAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    public SettlementComponentAuditResponse(IEnumerable<SettlementComponentAuditData> data, string serverAuditResult)
    {
        Data = data.ToArray();
        ServerAuditResult = serverAuditResult;
    }
}
