using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Alleys.Patches;

[HarmonyPatch(typeof(ArenaMasterCampaignBehavior))]
internal class DisableArenaMasterCampaignBehavior
{
    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
