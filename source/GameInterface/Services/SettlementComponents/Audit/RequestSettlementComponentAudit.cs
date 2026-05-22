using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.SettlementComponents.Audit;

/// <summary>
/// Command to request a settlementcomponent audit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record RequestSettlementComponentAudit : ICommand
{
    [ProtoMember(1)]
    public SettlementComponentAuditData[] Data { get; }
    public RequestSettlementComponentAudit(IEnumerable<SettlementComponentAuditData> data)
    {
        Data = data.ToArray();
    }
}