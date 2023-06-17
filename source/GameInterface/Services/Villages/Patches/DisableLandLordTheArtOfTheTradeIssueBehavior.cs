using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandLordTheArtOfTheTradeIssueBehavior))]
internal class DisableLandLordTheArtOfTheTradeIssueBehavior
{
    [HarmonyPatch(nameof(LandLordTheArtOfTheTradeIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
