using Common.Messaging;
using System.Collections.Generic;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// message used to change notable cache
/// </summary>
public record ChangeSettlementNotablesCache : ICommand
{
    public string SettlementId { get; }
    public List<string> NotablesCache { get; }

    public ChangeSettlementNotablesCache(string settlementId, List<string> notablesCache)
    {
        SettlementId = settlementId;
        NotablesCache = notablesCache;
    }
}
