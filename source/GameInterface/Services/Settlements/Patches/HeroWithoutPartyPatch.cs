using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Patches for AddHeroWithoutParty() => Server side sync
/// RemoveHeroWithoutParty() => Server side sync
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class HeroWithoutPartyPatch
{
    // TODO: discuss and test if needed, not sure...
    // only server needs to know about this..
    // may not be needed but for now if it does the code is here.
    [HarmonyPatch("AddHeroWithoutParty")]
    [HarmonyPrefix]
    private static bool AddHeroWithoutPartyPrefix(ref Settlement __instance, Hero individual) => ModInformation.IsServer;
    /*
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        var heroCache = __instance.GetHeroesWithoutPartyCache();

        if(!heroCache.Contains(individual))
        {
            heroCache.Add(individual);
            __instance.SetHeroesWithoutPartyCache(heroCache);

            var message = new SettlementChangedAddHeroWithoutParty(__instance.StringId, individual.StringId);

            MessageBroker.Instance.Publish(__instance, message);

            __instance.CollectNotablesToCache();
        }
        return false;
    }

    internal static void RunAddHeroWithoutParty(Settlement settlement, Hero individual)
    {

        // does this even need a allowedthread?
        var heroList = settlement.GetHeroesWithoutPartyCache();

        if (!heroList.Contains(individual))
        {
            heroList.Add(individual);
        }
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetHeroesWithoutPartyCache(heroList);
            }
        });
    }
    */

    [HarmonyPatch("RemoveHeroWithoutParty")]
    [HarmonyPrefix]
    private static bool RemoveHeroWithoutPartyPrefix(ref Settlement __instance, Hero individual) => ModInformation.IsServer;
    /*
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        var heroCache = __instance.GetHeroesWithoutPartyCache();

        if (heroCache.Contains(individual))
        {
            heroCache.Remove(individual);
            __instance.SetHeroesWithoutPartyCache(heroCache);

            var message = new SettlementChangedRemoveHeroWithoutParty(__instance.StringId, individual.StringId);

            MessageBroker.Instance.Publish(__instance, message);

            __instance.CollectNotablesToCache();
        }

        return false;
    }
    
    internal static void RunRemoveHeroWithoutParty(Settlement settlement, Hero individual)
    {

        // does this even need a allowedthread?
        var heroList = settlement.GetHeroesWithoutPartyCache();

        if (!heroList.Contains(individual))
        {
            heroList.Add(individual);
        }
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetHeroesWithoutPartyCache(heroList);
            }
        });
    }
    */
}
