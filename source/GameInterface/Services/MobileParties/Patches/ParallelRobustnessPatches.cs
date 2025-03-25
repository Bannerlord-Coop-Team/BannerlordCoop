using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignTickCacheDataStore))]
internal class ParallelRobustnessPatches
{
    static ILogger Logger = LogManager.GetLogger<ParallelRobustnessPatches>();


    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelCheckExitingSettlements))]
    [HarmonyPrefix]
    static bool ParallelCheckExitingSettlements(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int index = startInclusive; index < endExclusive; ++index)
        {
            MobileParty mobileParty = Campaign.Current.MobileParties[index];

            if (mobileParty.Party == null) continue;

            mobileParty.CheckExitingSettlementParallel(ref __instance._exitingSettlementCount, ref __instance._exitingSettlementMobilePartyList);
        }

        return false;
    }

    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelInitializeCachedPartyVariables))]
    [HarmonyPrefix]
    static bool ParallelInitializeCachedPartyVariables(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int index = startInclusive; index < endExclusive; ++index)
        {
            MobileParty mobileParty = Campaign.Current.MobileParties[index];

            if (mobileParty.Party == null) continue;

            __instance._cacheData[index].MobileParty = mobileParty;
            mobileParty.InitializeCachedPartyVariables(ref __instance._cacheData[index].LocalVariables);
        }

        return false;
    }

    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelCacheTargetPartyVariablesAtFrameStart))]
    [HarmonyPrefix]
    static bool TargetPartyVariables(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int i = startInclusive; i < endExclusive; i++)
        {
            var party = __instance._cacheData[i].MobileParty;

            if (party == null) continue;

            if (party.Ai == null)
            {
                Logger.Error("MobileParty with id {id} AI was null", party.StringId);
                continue;
            }

            party.Ai.CacheTargetPartyVariablesAtFrameStart(ref __instance._cacheData[i].LocalVariables);
        }

        return false;
    }

    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelArrangePartyIndices))]
    [HarmonyPrefix]
    static bool ParallelArrangePartyIndices(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int index = startInclusive; index < endExclusive; ++index)
        {
            MobileParty.CachedPartyVariables localVariables = __instance._cacheData[index].LocalVariables;
            if (localVariables.IsMoving)
            {
                if (localVariables.IsArmyLeader)
                    __instance._movingArmyLeaderPartyIndices[Interlocked.Increment(ref __instance._currentFrameMovingArmyLeaderCount)] = index;
                else
                    __instance._movingPartyIndices[Interlocked.Increment(ref __instance._currentFrameMovingPartyCount)] = index;
            }
            else
                __instance._stationaryPartyIndices[Interlocked.Increment(ref __instance._currentFrameStationaryPartyCount)] = index;
        }

        return false;
    }

    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelTickArmies))]
    [HarmonyPrefix]
    static bool ParallelTickArmies(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int index = startInclusive; index < endExclusive; ++index)
        {
            CampaignTickCacheDataStore.PartyTickCachePerParty tickCachePerParty = __instance._cacheData[__instance._movingArmyLeaderPartyIndices[index]];
            MobileParty mobileParty = tickCachePerParty.MobileParty;

            if (mobileParty.Party == null) continue;
            if (mobileParty.AttachedTo == null) continue;

            MobileParty.CachedPartyVariables localVariables = tickCachePerParty.LocalVariables;
            mobileParty.TickForMovingArmyLeader(ref localVariables, __instance._currentDt, __instance._currentRealDt);
            mobileParty.TickForMobileParty2(ref localVariables, __instance._currentRealDt, ref __instance._gridChangeCount, ref __instance._gridChangeMobilePartyList);
            mobileParty.ValidateSpeed();
        }

        return false;
    }

    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelTickMovingParties))]
    [HarmonyPrefix]
    static bool ParallelTickMovingParties(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int index = startInclusive; index < endExclusive; ++index)
        {
            CampaignTickCacheDataStore.PartyTickCachePerParty tickCachePerParty = __instance._cacheData[__instance._movingPartyIndices[index]];
            MobileParty mobileParty = tickCachePerParty.MobileParty;

            if (mobileParty.Party == null) continue;

            MobileParty.CachedPartyVariables localVariables = tickCachePerParty.LocalVariables;
            mobileParty.TickForMovingMobileParty(ref localVariables, __instance._currentDt, __instance._currentRealDt);
            mobileParty.TickForMobileParty2(ref localVariables, __instance._currentRealDt, ref __instance._gridChangeCount, ref __instance._gridChangeMobilePartyList);
        }

        return false;
    }


    [HarmonyPatch(nameof(CampaignTickCacheDataStore.ParallelTickStationaryParties))]
    [HarmonyPrefix]
    static bool ParallelTickStationaryParties(CampaignTickCacheDataStore __instance, int startInclusive, int endExclusive)
    {
        for (int index = startInclusive; index < endExclusive; ++index)
        {
            CampaignTickCacheDataStore.PartyTickCachePerParty tickCachePerParty = __instance._cacheData[__instance._stationaryPartyIndices[index]];
            MobileParty mobileParty = tickCachePerParty.MobileParty;

            if (mobileParty?.Party == null) continue;

            MobileParty.CachedPartyVariables localVariables = tickCachePerParty.LocalVariables;
            mobileParty.TickForStationaryMobileParty(ref localVariables, __instance._currentDt, __instance._currentRealDt);
            mobileParty.TickForMobileParty2(ref localVariables, __instance._currentRealDt, ref __instance._gridChangeCount, ref __instance._gridChangeMobilePartyList);
        }

        return false;
    }
}
