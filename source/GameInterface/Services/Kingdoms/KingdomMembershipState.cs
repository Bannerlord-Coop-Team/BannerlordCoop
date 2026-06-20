using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms;

public interface IKingdomMembershipState
{
    void EnsureClanInKingdom(Kingdom kingdom, Clan clan, bool publishCollectionChanges);
    void MoveClanToKingdom(Kingdom previousKingdom, Kingdom kingdom, Clan clan, bool publishCollectionChanges);
}

internal class KingdomMembershipState : IKingdomMembershipState
{
    public void EnsureClanInKingdom(Kingdom kingdom, Clan clan, bool publishCollectionChanges)
    {
        MoveClanToKingdom(clan?.Kingdom, kingdom, clan, publishCollectionChanges);
    }

    public void MoveClanToKingdom(
        Kingdom previousKingdom,
        Kingdom kingdom,
        Clan clan,
        bool publishCollectionChanges)
    {
        if (kingdom == null || clan == null) return;

        var clanFiefs = GetClanFiefs(clan);
        if (previousKingdom != null && previousKingdom != kingdom)
        {
            EnsureRuntimeCollections(previousKingdom);
            RemoveClanState(previousKingdom, clan, clanFiefs, publishCollectionChanges);
        }

        EnsureRuntimeCollections(kingdom);
        AddClanState(kingdom, clan, clanFiefs, publishCollectionChanges);
    }

    private static void AddClanState(
        Kingdom kingdom,
        Clan clan,
        IReadOnlyList<Town> clanFiefs,
        bool publishCollectionChanges)
    {
        KingdomCollectionSync.AddClan(kingdom, clan, publishCollectionChanges);

        foreach (var fief in clanFiefs)
        {
            AddFiefState(kingdom, fief, publishCollectionChanges);
        }
    }

    private static void RemoveClanState(
        Kingdom kingdom,
        Clan clan,
        IReadOnlyList<Town> clanFiefs,
        bool publishCollectionChanges)
    {
        KingdomCollectionSync.RemoveClan(kingdom, clan, publishCollectionChanges);

        foreach (var fief in clanFiefs)
        {
            RemoveFiefState(kingdom, fief, publishCollectionChanges);
        }
    }

    private static void AddFiefState(Kingdom kingdom, Town fief, bool publishCollectionChanges)
    {
        if (fief == null) return;

        KingdomCollectionSync.AddFief(kingdom, fief, publishCollectionChanges);
    }

    private static void RemoveFiefState(Kingdom kingdom, Town fief, bool publishCollectionChanges)
    {
        if (fief == null) return;

        KingdomCollectionSync.RemoveFief(kingdom, fief, publishCollectionChanges);
    }

    private static IReadOnlyList<Town> GetClanFiefs(Clan clan)
    {
        if (clan == null) return Array.Empty<Town>();

        var fiefs = new List<Town>();
        try
        {
            if (clan.Fiefs != null)
            {
                fiefs.AddRange(clan.Fiefs.Where(fief => fief != null));
            }
        }
        catch (Exception)
        {
        }

        if (clan._fiefsCache != null)
        {
            fiefs.AddRange(clan._fiefsCache.Where(fief => fief != null));
        }

        return fiefs.Distinct().ToArray();
    }

    private static void EnsureRuntimeCollections(Kingdom kingdom)
    {
        KingdomRegistry.EnsureRuntimeCollections(kingdom);
    }
}
