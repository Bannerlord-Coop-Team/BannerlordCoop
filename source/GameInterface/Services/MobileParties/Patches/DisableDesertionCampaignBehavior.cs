using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DesertionCampaignBehavior))]
internal class DisableDesertionCampaignBehavior
{
    [HarmonyPatch(nameof(DesertionCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
