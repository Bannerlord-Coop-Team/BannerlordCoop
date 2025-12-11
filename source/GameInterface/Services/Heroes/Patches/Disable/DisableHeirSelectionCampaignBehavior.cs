using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class DisableHeirSelectionCampaignBehavior
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
