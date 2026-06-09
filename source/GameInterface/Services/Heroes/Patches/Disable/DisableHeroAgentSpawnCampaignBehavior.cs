using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;


[HarmonyPatch(typeof(HeroAgentSpawnCampaignBehavior))]
internal class DisableHeroAgentSpawnCampaignBehavior
{
    // Needs to also run on the client
    [HarmonyPatch(nameof(HeroAgentSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix() => true;
}
