using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(FamilyFeudIssueBehavior))]
internal class DisableFamilyFeudIssueBehavior
{
    [HarmonyPatch(nameof(FamilyFeudIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
