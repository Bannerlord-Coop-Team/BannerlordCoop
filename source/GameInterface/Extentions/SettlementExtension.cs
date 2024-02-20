using Coop.Mod.Extentions;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;

namespace GameInterface.Extentions;

/// <summary>
/// Adds additional reflection functions for Settlements
/// </summary>
internal static class SettlementExtension
{
    private static readonly PropertyInfo Settlement_LastThreatTime = typeof(Settlement).GetProperty(nameof(Settlement.LastThreatTime));
    private static readonly PropertyInfo Settlement_HitPoints = typeof(Settlement).GetProperty(nameof(Settlement.SettlementHitPoints));

    private static readonly PropertyInfo Settlement_CurrentSiegeState = typeof(Settlement).GetProperty(nameof(Settlement.CurrentSiegeState));
    private static readonly PropertyInfo Settlemnent_GarrisonWagePaymentLimit = typeof(Settlement).GetProperty(nameof(Settlement.GarrisonWagePaymentLimit));

    private static readonly FieldInfo _notablesCache = typeof(Settlement).GetField("_notablesCache", BindingFlags.NonPublic | BindingFlags.Instance);
    private static MethodInfo Settlement_CollectNotablesToCache = AccessTools.Method(typeof(Settlement), "CollectNotablesToCache");


    private static readonly FieldInfo _heroesWithoutPartyCache = typeof(Settlement).GetField("_heroesWithoutPartyCache", BindingFlags.NonPublic | BindingFlags.Instance);
    private static MethodInfo Settlement_AddHeroWithoutParty = AccessTools.Method(typeof(Settlement), "AddHeroWithoutParty");


    private static readonly FieldInfo _partiesCache = typeof(Settlement).GetField("_partiesCache", BindingFlags.NonPublic | BindingFlags.Instance);
    private static MethodInfo Settlement_AddMobileParty = AccessTools.Method(typeof(Settlement), "AddMobileParty");

    private static readonly FieldInfo _numberOfLordPartiesAt = typeof(Settlement).GetField("_numberOfLordPartiesAt", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo _settlementWallSectionHitPointsRatioList = typeof(Settlement).GetField("._settlementWallSectionHitPointsRatioList", BindingFlags.NonPublic | BindingFlags.Instance);



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

    public static MBList<Hero> GetNotablesCache(this Settlement component)
    {
        return (MBList<Hero>)_notablesCache.GetValue(component);
    }

    public static void SetNotableCache(this Settlement component, MBList<Hero> heros)
    {
        _notablesCache.SetValue(component, heros);
    }

    public static void CollectNotablesToCache(this Settlement component)
    {
        Settlement_CollectNotablesToCache.Invoke(component, new object[] { });
    }

    public static MBList<Hero> GetHeroesWithoutPartyCache(this Settlement component)
    {
        return (MBList<Hero>)_heroesWithoutPartyCache.GetValue(component);
    }

    public static void SetHeroesWithoutPartyCache(this Settlement component, MBList<Hero> heros)
    {
        _heroesWithoutPartyCache.SetValue(component, heros);
    }

    public static void AddHeroWithoutPartyCache(this Settlement component, Hero hero)
    {
        Settlement_AddHeroWithoutParty.Invoke(component, new object[] { hero });
    }


    public static MBList<MobileParty> GetPartiesCache(this Settlement component)
    {
        return (MBList<MobileParty>)_partiesCache.GetValue(component);
    }
    public static void SetPartiesCache(this Settlement component, MBList<MobileParty> mobileParties)
    {
        _partiesCache.SetValue(component, mobileParties);
    }

    public static void AddMobileParty(this Settlement component, MobileParty party)
    {
        Settlement_AddMobileParty.Invoke(component, new object[] { party });
    }

    public static int GetNumberOfLordPartiesAt(this Settlement component)
    {
        return (int)_numberOfLordPartiesAt.GetValue(component);
    }

    public static void SetNumberOfLordPartiesAt(this Settlement component, int lordParties)
    {
        _numberOfLordPartiesAt.SetValue(component, lordParties);
    }

    public static MBList<float> GetSettlementWallSectionHitPointsRatioList(this Settlement component)
    {
        return (MBList<float>)_settlementWallSectionHitPointsRatioList.GetValue(component);
    }
}
