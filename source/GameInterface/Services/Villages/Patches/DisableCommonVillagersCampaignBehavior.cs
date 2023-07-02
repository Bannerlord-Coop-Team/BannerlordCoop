using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(CommonVillagersCampaignBehavior))]
internal class DisableCommonVillagersCampaignBehavior
{
    [HarmonyPatch(nameof(CommonVillagersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
