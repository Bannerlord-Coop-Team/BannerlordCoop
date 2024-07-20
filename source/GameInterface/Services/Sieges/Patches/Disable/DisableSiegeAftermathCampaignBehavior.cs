using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Sieges.Patches.Disable;

[HarmonyPatch(typeof(SiegeAftermathCampaignBehavior))]
internal class DisableSiegeAftermathCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeAftermathCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
