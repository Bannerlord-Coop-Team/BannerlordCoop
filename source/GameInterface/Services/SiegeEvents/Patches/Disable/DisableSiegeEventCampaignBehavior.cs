using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.SiegeEvents.Patches.Disable;

[HarmonyPatch(typeof(SiegeEventCampaignBehavior))]
internal class DisableSiegeEventCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
