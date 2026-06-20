using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Alleys.Patches;

[HarmonyPatch(typeof(AlleyCampaignBehavior))]
internal class DisableAlleyCampaignBehavior
{
    [HarmonyPatch(nameof(AlleyCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
