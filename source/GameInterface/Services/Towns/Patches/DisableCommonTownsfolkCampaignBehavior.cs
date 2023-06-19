using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(CommonTownsfolkCampaignBehavior))]
internal class DisableCommonTownsfolkCampaignBehavior
{
    [HarmonyPatch(nameof(CommonTownsfolkCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
