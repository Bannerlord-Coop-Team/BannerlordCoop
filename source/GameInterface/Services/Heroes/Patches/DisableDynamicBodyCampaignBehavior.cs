using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(DynamicBodyCampaignBehavior))]
internal class DisableDynamicBodyCampaignBehavior
{
    [HarmonyPatch(nameof(DynamicBodyCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
