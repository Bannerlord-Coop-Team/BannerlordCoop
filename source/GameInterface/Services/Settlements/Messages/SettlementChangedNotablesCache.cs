using Common.Logging.Attributes;
using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// When the settlement notable cache changes
/// </summary>
/// 
[BatchLogMessage]
public readonly struct SettlementChangedNotablesCache : IEvent
{
    public readonly Settlement Settlement;
    public readonly List<string> NotablesCache;

    public SettlementChangedNotablesCache(Settlement settlement, List<string> notablesCache)
    {
        Settlement = settlement;
        NotablesCache = notablesCache;
    }
}
