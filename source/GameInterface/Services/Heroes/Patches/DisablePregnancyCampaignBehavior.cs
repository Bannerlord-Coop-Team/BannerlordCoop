using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(PregnancyCampaignBehavior))]
internal class DisablePregnancyCampaignBehavior
{
    [HarmonyPatch(nameof(PregnancyCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
