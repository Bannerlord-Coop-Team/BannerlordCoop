using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(TavernEmployeesCampaignBehavior))]
internal class DisableTavernEmployeesCampaignBehavior
{
    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
