using Coop.Mod.Extentions;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;

namespace GameInterface.Extentions;
internal static class SettlementExtension
{
    private static readonly PropertyInfo Settlement_LastThreatTime = typeof(Settlement).GetProperty(nameof(Settlement.LastThreatTime));
    private static readonly PropertyInfo Settlement_HitPoints = typeof(Settlement).GetProperty(nameof(Settlement.SettlementHitPoints));

    private static readonly PropertyInfo Settlement_CurrentSiegeState = typeof(Settlement).GetProperty(nameof(Settlement.CurrentSiegeState));
    private static readonly PropertyInfo Settlemnent_GarrisonWagePaymentLimit = typeof(Settlement).GetProperty(nameof(Settlement.GarrisonWagePaymentLimit));



    public static void SetLastThreatTimeChanged(this Settlement component, long? lastThreatTime)
    {
        if (lastThreatTime.HasValue)
            component.LastThreatTime.SetNumTicks(lastThreatTime.Value);
        else
            Settlement_LastThreatTime.SetValue(component, lastThreatTime);
    }

    public static void SetHitPointsChanged(this Settlement component, float settlementHitPoints)
    {
        Settlement_HitPoints.SetValue(component, settlementHitPoints);
    }

    public static void SetSiegeState(this Settlement component, SiegeState siegeState)
    {
        Settlement_CurrentSiegeState.SetValue(component, siegeState);
    }
}
