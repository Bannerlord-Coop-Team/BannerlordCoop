using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(GuardsCampaignBehavior))]
internal class DisableGuardsCampaignBehavior
{
    [HarmonyPatch(nameof(GuardsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
