using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(EducationCampaignBehavior))]
internal class DisableEducationCampaignBehavior
{
    [HarmonyPatch(nameof(EducationCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
