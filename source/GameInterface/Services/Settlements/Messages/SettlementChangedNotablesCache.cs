using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// When the settlement notable cache changes
/// </summary>
/// 
public readonly struct SettlementChangedNotablesCache : IEvent
{
    public readonly Settlement Settlement;
    public readonly List<Hero> NotablesCache;

    public SettlementChangedNotablesCache(Settlement settlement, List<Hero> notablesCache)
    {
        Settlement = settlement;
        NotablesCache = notablesCache;
    }
}
