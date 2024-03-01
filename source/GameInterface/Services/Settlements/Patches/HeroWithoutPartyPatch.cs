using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<HeroWithoutPartyPatch>();

    [HarmonyPatch("AddHeroWithoutParty")]
    [HarmonyPrefix]
    private static bool AddHeroWithoutPartyPrefix(ref Settlement __instance, Hero individual)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;

        var heroCache = __instance.GetHeroesWithoutPartyCache();

        if(!heroCache.Contains(individual))
        {

            var message = new SettlementChangedAddHeroWithoutParty(__instance.StringId, individual.StringId);

            MessageBroker.Instance.Publish(__instance, message);

        }
        return true;
    }

    internal static void RunAddHeroWithoutParty(Settlement settlement, Hero individual)
    {

        // does this even need a allowedthread?
        var heroList = settlement.GetHeroesWithoutPartyCache();

        if (!heroList.Contains(individual))
        {
            heroList.Add(individual);
        } 
        else
        {
            Logger.Error("Attempted to add Hero {HeroId} that was already in list", individual.StringId);
        }
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetHeroesWithoutPartyCache(heroList);
            }
        });
    }


    [HarmonyPatch("RemoveHeroWithoutParty")]
    [HarmonyPrefix]
    private static bool RemoveHeroWithoutPartyPrefix(ref Settlement __instance, Hero individual)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;

        var heroCache = __instance.GetHeroesWithoutPartyCache();

        if (heroCache.Contains(individual))
        {

            var message = new SettlementChangedRemoveHeroWithoutParty(__instance.StringId, individual.StringId);

            MessageBroker.Instance.Publish(__instance, message);
        }

        return true;
    }
    
    internal static void RunRemoveHeroWithoutParty(Settlement settlement, Hero individual)
    {

        var heroList = settlement.GetHeroesWithoutPartyCache();

        if (heroList.Contains(individual))
        {
            heroList.Remove(individual);
        }
        else
        {
            Logger.Error("Attempted to Remove Hero {HeroId} that is not in list", individual.StringId);
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetHeroesWithoutPartyCache(heroList);
            }
        });
    }
  
}
