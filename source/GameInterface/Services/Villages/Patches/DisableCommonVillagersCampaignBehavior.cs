using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(CommonVillagersCampaignBehavior))]
internal class DisableCommonVillagersCampaignBehavior
{
    [HarmonyPatch(nameof(CommonVillagersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
