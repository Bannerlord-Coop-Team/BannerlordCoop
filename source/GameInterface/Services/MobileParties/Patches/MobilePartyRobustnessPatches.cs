using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyRobustnessPatches
{
    [HarmonyPatch(nameof(MobileParty.Anchor), MethodType.Getter)]
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
}

/// <summary>
/// Guards against NullReferenceException in <see cref="WarPartyComponent.Clan"/>
/// when <see cref="PartyComponent.MobileParty"/> is null during a multiplayer sync
/// transition, which causes <see cref="WarPartyComponent.GetDefaultComponentBanner"/>
/// to NRE before it can null-check the result.
/// </summary>
[HarmonyPatch(typeof(WarPartyComponent), nameof(WarPartyComponent.GetDefaultComponentBanner))]
internal class WarPartyComponentBannerRobustnessPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<WarPartyComponentBannerRobustnessPatch>();

    [HarmonyPrefix]
    private static bool Prefix(WarPartyComponent __instance, ref Banner __result)
    {
        if (__instance.MobileParty == null)
        {
            Logger.Debug("WarPartyComponent.GetDefaultComponentBanner: MobileParty is null, returning null banner");
            __result = null;
            return false;
        }
        return true;
    }
}
