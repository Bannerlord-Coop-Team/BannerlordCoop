using Common.Messaging;
using GameInterface.Services.Settlements.Audit;
using ProtoBuf;

namespace Coop.Core.Client.Services.Settlements.Messages;


/// <summary>
/// To request an settlement audit
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkRequestSettlementAudit : ICommand
{
    [ProtoMember(1)]
    public SettlementAuditData[] Data { get; }

    public NetworkRequestSettlementAudit(SettlementAuditData[] data)
    {
        Data = data;
    }
}
