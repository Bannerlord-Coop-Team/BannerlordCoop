using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;


[HarmonyPatch(typeof(HeroAgentSpawnCampaignBehavior))]
internal class DisableHeroAgentSpawnCampaignBehavior
{
    [HarmonyPatch(nameof(HeroAgentSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
