using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(PregnancyCampaignBehavior))]
internal class DisablePregnancyCampaignBehavior
{
    [HarmonyPatch(nameof(PregnancyCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
