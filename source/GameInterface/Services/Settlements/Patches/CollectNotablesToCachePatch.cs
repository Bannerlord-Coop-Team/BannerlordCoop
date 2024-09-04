using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Patch for CollectNotablesToCache() function
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class CollectNotablesToCachePatch
{
    // only server needs to know about this..
    // may not be needed but for now if it does the code is here.
    [HarmonyPatch("CollectNotablesToCache")]
    [HarmonyPrefix]
    private static bool CollectNotablesToCachePrefix(ref Settlement __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;

        var notableCache = __instance._notablesCache;
        notableCache.Clear();

        foreach (Hero hero in __instance.HeroesWithoutParty)
        {
            if (hero.IsNotable)
            {
                notableCache.Add(hero);
            }
        }


        //pub list to server
        List<string> cacheHeros = new();

        notableCache.ForEach(hero => cacheHeros.Add(hero.StringId));

        var message = new SettlementChangedNotablesCache(__instance.StringId, cacheHeros);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }

    internal static void RunNotablesCacheChange(Settlement settlement, MBList<Hero> heroes)
    {

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement._notablesCache = heroes;
            }
        });
    }
}
