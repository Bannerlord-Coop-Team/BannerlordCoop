using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;


[HarmonyPatch(typeof(HeroKnownInformationCampaignBehavior))]
internal class DisableHeroKnownInformationCampaignBehavior
{
    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
