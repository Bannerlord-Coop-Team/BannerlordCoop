using Common.Util;
using GameInterface.Utils;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms;

internal static class KingdomCollectionSync
{
    public static void AddArmy(Kingdom kingdom, Army army, bool publish)
    {
        if (Add(kingdom, nameof(Kingdom._armies), ref kingdom._armies, army, publish)
            && army.Kingdom != kingdom)
        {
            SetField(army, "_kingdom", kingdom, publish);
        }
    }

    public static void RemoveArmy(Kingdom kingdom, Army army, bool publish)
    {
        if (Remove(kingdom, nameof(Kingdom._armies), kingdom._armies, army, publish)
            && army.Kingdom == kingdom)
        {
            SetField<Army, Kingdom>(army, "_kingdom", null, publish);
        }
    }

    public static void AddClan(Kingdom kingdom, Clan clan, bool publish)
    {
        if (kingdom == null || clan == null) return;

        var previousKingdom = clan.Kingdom;
        if (previousKingdom != null && previousKingdom != kingdom)
        {
            RemoveClan(previousKingdom, clan, publish);
        }

        Add(kingdom, nameof(Kingdom._clans), ref kingdom._clans, clan, publish);
        if (clan.Kingdom != kingdom)
        {
            SetField(clan, nameof(Clan._kingdom), kingdom, publish);
        }
    }

    public static void RemoveClan(Kingdom kingdom, Clan clan, bool publish)
    {
        Remove(kingdom, nameof(Kingdom._clans), kingdom._clans, clan, publish);
        if (clan.Kingdom == kingdom)
        {
            SetField<Clan, Kingdom>(clan, nameof(Clan._kingdom), null, publish);
        }
    }

    public static void AddFief(Kingdom kingdom, Town town, bool publish)
    {
        if (town == null) return;

        Add(kingdom, nameof(Kingdom._fiefsCache), ref kingdom._fiefsCache, town, publish);
        if (IsTown(town))
        {
            AddTown(kingdom, town, publish);
        }

        var settlement = GetSettlement(town);
        if (settlement != null)
        {
            AddSettlement(kingdom, settlement, publish);
        }
    }

    public static void RemoveFief(Kingdom kingdom, Town town, bool publish)
    {
        if (town == null) return;

        Remove(kingdom, nameof(Kingdom._fiefsCache), kingdom._fiefsCache, town, publish);
        if (IsTown(town))
        {
            RemoveTown(kingdom, town, publish);
        }

        var settlement = GetSettlement(town);
        if (settlement != null)
        {
            RemoveSettlement(kingdom, settlement, publish);
        }
    }

    public static void AddHero(Kingdom kingdom, Hero hero, bool publish) =>
        Add(kingdom, nameof(Kingdom._heroesCache), ref kingdom._heroesCache, hero, publish);

    public static void RemoveHero(Kingdom kingdom, Hero hero, bool publish) =>
        Remove(kingdom, nameof(Kingdom._heroesCache), kingdom._heroesCache, hero, publish);

    public static void AddAliveLord(Kingdom kingdom, Hero hero, bool publish) =>
        Add(kingdom, nameof(Kingdom._aliveLordsCache), ref kingdom._aliveLordsCache, hero, publish);

    public static void RemoveAliveLord(Kingdom kingdom, Hero hero, bool publish) =>
        Remove(kingdom, nameof(Kingdom._aliveLordsCache), kingdom._aliveLordsCache, hero, publish);

    public static void AddDeadLord(Kingdom kingdom, Hero hero, bool publish) =>
        Add(kingdom, nameof(Kingdom._deadLordsCache), ref kingdom._deadLordsCache, hero, publish);

    public static void RemoveDeadLord(Kingdom kingdom, Hero hero, bool publish) =>
        Remove(kingdom, nameof(Kingdom._deadLordsCache), kingdom._deadLordsCache, hero, publish);

    public static void AddSettlement(Kingdom kingdom, Settlement settlement, bool publish) =>
        Add(kingdom, nameof(Kingdom._settlementsCache), ref kingdom._settlementsCache, settlement, publish);

    public static void RemoveSettlement(Kingdom kingdom, Settlement settlement, bool publish) =>
        Remove(kingdom, nameof(Kingdom._settlementsCache), kingdom._settlementsCache, settlement, publish);

    public static void AddTown(Kingdom kingdom, Town town, bool publish) =>
        Add(kingdom, nameof(Kingdom._townsCache), ref kingdom._townsCache, town, publish);

    public static void RemoveTown(Kingdom kingdom, Town town, bool publish) =>
        Remove(kingdom, nameof(Kingdom._townsCache), kingdom._townsCache, town, publish);

    public static void AddVillage(Kingdom kingdom, Village village, bool publish) =>
        Add(kingdom, nameof(Kingdom._villagesCache), ref kingdom._villagesCache, village, publish);

    public static void RemoveVillage(Kingdom kingdom, Village village, bool publish) =>
        Remove(kingdom, nameof(Kingdom._villagesCache), kingdom._villagesCache, village, publish);

    public static void AddWarPartyComponent(Kingdom kingdom, WarPartyComponent warPartyComponent, bool publish) =>
        Add(kingdom, nameof(Kingdom._warPartyComponentsCache), ref kingdom._warPartyComponentsCache, warPartyComponent, publish);

    public static void RemoveWarPartyComponent(Kingdom kingdom, WarPartyComponent warPartyComponent, bool publish) =>
        Remove(kingdom, nameof(Kingdom._warPartyComponentsCache), kingdom._warPartyComponentsCache, warPartyComponent, publish);

    private static bool Add<T>(Kingdom kingdom, string fieldName, ref MBList<T> list, T value, bool publish)
        where T : class
    {
        if (kingdom == null || value == null) return false;

        list ??= new MBList<T>();
        if (list.Contains(value)) return false;

        if (publish && TryGetCollectionIntercept(fieldName, isAdd: true, out var intercept))
        {
            using (AllowedThread.Suspend())
            {
                intercept.Invoke(null, new object[] { list, value, kingdom });
            }
        }
        else
        {
            list.Add(value);
        }

        return true;
    }

    private static bool Remove<T>(Kingdom kingdom, string fieldName, MBList<T> list, T value, bool publish)
        where T : class
    {
        if (kingdom == null || value == null || list == null || !list.Contains(value)) return false;

        if (publish && TryGetCollectionIntercept(fieldName, isAdd: false, out var intercept))
        {
            using (AllowedThread.Suspend())
            {
                intercept.Invoke(null, new object[] { list, value, kingdom });
            }
        }
        else
        {
            list.Remove(value);
        }

        return true;
    }

    private static void SetField<TInstance, TValue>(TInstance instance, string fieldName, TValue value, bool publish)
        where TInstance : class
    {
        if (instance == null) return;

        var field = AccessTools.Field(typeof(TInstance), fieldName);
        if (field == null) return;

        if (publish && GenericPatchHelpers.FieldInterceptCache.TryGetValue(field, out var intercept))
        {
            using (AllowedThread.Suspend())
            {
                intercept.Invoke(null, new object[] { instance, value });
            }
        }
        else
        {
            field.SetValue(instance, value);
        }
    }

    private static bool TryGetCollectionIntercept(string fieldName, bool isAdd, out MethodInfo intercept)
    {
        var field = AccessTools.Field(typeof(Kingdom), fieldName);
        var cache = isAdd
            ? GenericPatchHelpers.CollectionAddInterceptCache
            : GenericPatchHelpers.CollectionRemoveInterceptCache;

        return cache.TryGetValue(field, out intercept);
    }

    private static Settlement GetSettlement(Town town)
    {
        try
        {
            return town?.Settlement;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsTown(Town town)
    {
        try
        {
            return town?.IsTown == true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
