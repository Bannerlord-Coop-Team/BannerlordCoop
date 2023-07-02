using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(ExtortionByDesertersIssueBehavior))]
internal class DisableExtortionByDesertersIssueBehavior
{
    [HarmonyPatch(nameof(ExtortionByDesertersIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
