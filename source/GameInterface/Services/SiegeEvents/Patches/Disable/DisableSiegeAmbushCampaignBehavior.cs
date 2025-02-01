using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.SiegeEvents.Patches.Disable;

[HarmonyPatch(typeof(SiegeAmbushCampaignBehavior))]
internal class DisableSiegeAmbushCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeAmbushCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
