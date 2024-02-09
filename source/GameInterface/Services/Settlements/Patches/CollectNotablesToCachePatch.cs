using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
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
    [HarmonyPatch("CollectNotablesToCache")]
    [HarmonyPrefix]
    private static bool CollectNotablesToCachePrefix(ref Settlement __instance)
    {

        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        var notableCache = __instance.GetNotablesCache();
        notableCache.Clear();

        foreach(Hero hero in __instance.HeroesWithoutParty)
        {
            if(hero.IsNotable)
            {
                notableCache.Add(hero);
            }
        }

        __instance.SetNotableCache(notableCache);

        //pub list to server
        List<string> cacheHeros = new();

        notableCache.ForEach(hero => cacheHeros.Add(hero.StringId));

        var message = new SettlementChangedNotablesCache(__instance.StringId, cacheHeros);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }


    internal static void RunNotablesCacheChange(Settlement settlement, MBList<Hero> heros)
    {

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetNotableCache(heros);
            }
        });
    }
}
