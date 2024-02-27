using Common.Messaging;
using GameInterface.Services.Settlements.Audit;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkSettlementAuditResults : IEvent
{
    [ProtoMember(1)]
    public SettlementAuditData[] Data { get; }

    [ProtoMember(2)]
    public string ServerAuditResults { get; }

    public NetworkSettlementAuditResults(SettlementAuditData[] data, string serverAuditResults)
    {
        Data = data;
        ServerAuditResults = serverAuditResults;
    }
}
