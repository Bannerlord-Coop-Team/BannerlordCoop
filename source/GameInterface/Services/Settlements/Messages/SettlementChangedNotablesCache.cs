using Common.Logging.Attributes;
using Common.Messaging;
using System.Collections.Generic;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// When the settlement notable cache changes
/// </summary>
/// 
[BatchLogMessage]
public record SettlementChangedNotablesCache : IEvent
{
    public string SettlementId { get; }
    public List<string> NotablesCache { get; }

    public SettlementChangedNotablesCache(string settlementId, List<string> notablesCache)
    {
        SettlementId = settlementId;
        NotablesCache = notablesCache;
    }
}
