using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Village;


namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// Disables all functionality for Town
/// </summary>
[HarmonyPatch(typeof(Village))]
internal class VillagePatches
{
    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix() => ModInformation.IsServer;


    [HarmonyPatch(nameof(Village.VillageState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool VillageStatePrefix(ref Village __instance, ref VillageStates value)
    {
        if(AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;
        if (__instance._villageState == value) return false;
        
        var message = new VillageStateChanged(__instance.StringId, (int)value);
        MessageBroker.Instance.Publish(__instance, message);    
        return true;
    }

    public static void RunVillageStateChange(Village village, VillageStates state)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using(new AllowedThread())
            {
                village.VillageState = state;
                village.Settlement.Party.SetLevelMaskIsDirty();
            }
        });
    }

    // Justification:
    // At the moment looks like we dont need to handle this as the Villages that are bounded should never change.
    // But good for more investigating soon.
    [HarmonyPatch(nameof(Village.Bound), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool BoundPrefix() => true;


    [HarmonyPatch(nameof(Village.Hearth), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool HearthPrefix(ref Village __instance, ref float value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var message = new VillageHearthChanged(__instance.StringId, value);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    public static void ChangeHearth(Village village, float Hearth)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                village.Hearth = Hearth;
            }
        });
    }

    [HarmonyPatch(nameof(Village.TradeBound), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool TradeBoundPrefix(ref Village __instance, ref Settlement value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        if (__instance._tradeBound == value) return false;

        var message = new VillageTradeBoundChanged(__instance.StringId, value.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static void RunTradeBoundChange(Village village, Settlement tradebound)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                village.TradeBound = tradebound;
            }
        });
    }


    [HarmonyPatch(nameof(Village.TradeTaxAccumulated), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool TradeTaxAccumulatedPrefix(ref Village __instance, ref int value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var message = new VillageTaxAccumulateChanged(__instance.StringId, value);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    internal static void RunTradeTaxChange(Village village, int tradeTaxAccumulated)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                village.TradeTaxAccumulated = tradeTaxAccumulated;
            }
        });
    }

    [HarmonyPatch(nameof(Village.LastDemandSatisfiedTime), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool LastDemandSatisifiedTimePrefix(ref Village __instance, ref float value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var message = new VillageDemandTimeChanged(__instance.StringId, value);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    internal static void RunLastDemandTimeSatisified(Village village, float LastDemandSatisfiedTime)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                village.LastDemandSatisfiedTime = LastDemandSatisfiedTime;
            }
        });
    }
}
