using Common.Messaging;
using GameInterface.Services.Settlements.Audit;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Response message for the settlement audit.
/// </summary>
/// 
[ProtoContract(SkipConstructor = true)]
internal record NetworkSettlementAuditResponse : IEvent
{
    [ProtoMember(1)]
    public SettlementAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResults { get; }

    public NetworkSettlementAuditResponse(SettlementAuditData[] data, string serverAuditResults)
    {
        Data = data;
        ServerAuditResults = serverAuditResults;
    }
}
