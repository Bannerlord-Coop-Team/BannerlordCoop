using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Buildings.Patches;

[HarmonyPatch(typeof(BuildingsCampaignBehavior))]
internal class DisableBuildingsCampaignBehavior
{
    [HarmonyPatch(nameof(BuildingsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
