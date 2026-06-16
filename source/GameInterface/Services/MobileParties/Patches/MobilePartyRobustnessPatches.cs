using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class MobilePartyRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyRobustnessPatches>();

    private static readonly FieldInfo NavigationCacheField =
        AccessTools.Field(typeof(DefaultMapDistanceModel), "_navigationCache");
    private static bool _loggedNullNavigationCache;

    // _navigationCache can be null on clients; the original then NREs. Fall back to 0 distance for now.
    [HarmonyPatch(typeof(DefaultMapDistanceModel), nameof(DefaultMapDistanceModel.GetDistance),
        new[] { typeof(Settlement), typeof(Settlement), typeof(bool), typeof(bool), typeof(MobileParty.NavigationType), typeof(float) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyPrefix]
    private static bool Prefix_GetDistance(DefaultMapDistanceModel __instance, ref float __result, ref float landRatio)
    {
        if (NavigationCacheField.GetValue(__instance) != null) return true;

        if (!_loggedNullNavigationCache)
        {
            _loggedNullNavigationCache = true;
            Logger.Warning("DefaultMapDistanceModel._navigationCache is null; returning 0 distance (logged once)");
        }

        __result = 0f;
        landRatio = 1f;
        return false;
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.Anchor), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Postfix(ref MobileParty __instance, ref AnchorPoint __result)
    {
        if (__result is null)
        {
            var anchor = new AnchorPoint(__instance);
            __instance.Anchor = anchor;
            __result = anchor;
        }
    }

    [HarmonyPatch(typeof(WarPartyComponent), nameof(WarPartyComponent.GetDefaultComponentBanner))]
    [HarmonyPrefix]
    private static bool Prefix(WarPartyComponent __instance, ref Banner __result)
    {
        if (__instance.MobileParty == null)
        {
            Logger.Error("WarPartyComponent.GetDefaultComponentBanner: MobileParty is null, returning null banner");
            __result = null;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel), nameof(DefaultPartySpeedCalculatingModel.CalculateFinalSpeed))]
    [HarmonyFinalizer]
    private static Exception Finalizer_CalculateFinalSpeed(Exception __exception, MethodBase __originalMethod)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"{__originalMethod.DeclaringType}.{__originalMethod.Name}");
        }

        return null;
    }
}