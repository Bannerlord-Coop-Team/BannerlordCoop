using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.SettlementComponents.Audit;

/// <summary>
/// Command to request a settlementcomponent audit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record RequestSettlementComponentAudit : ICommand
{
    [ProtoMember(1)]
    public SettlementComponentAuditData[] Data { get; }

    public RequestSettlementComponentAudit(SettlementComponentAuditData[] data)
    {
        Data = data;
    }
}