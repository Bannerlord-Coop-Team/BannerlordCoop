using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GuardsCampaignBehavior))]
internal class DisableGuardsCampaignBehavior
{
    [HarmonyPatch(nameof(GuardsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
