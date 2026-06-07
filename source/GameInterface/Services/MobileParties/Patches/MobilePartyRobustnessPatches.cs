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
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class MobilePartyRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyRobustnessPatches>();

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.Anchor), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Postfix(ref MobileParty __instance, ref AnchorPoint __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
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
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
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