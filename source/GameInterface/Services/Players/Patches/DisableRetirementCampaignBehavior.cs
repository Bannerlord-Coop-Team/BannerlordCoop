using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(RetirementCampaignBehavior))]
internal class DisableRetirementCampaignBehavior
{
    [HarmonyPatch(nameof(RetirementCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
