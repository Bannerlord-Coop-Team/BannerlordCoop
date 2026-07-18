using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(HeroAgentSpawnCampaignBehavior))]
internal class DisableHeroAgentSpawnCampaignBehavior
{
    [HarmonyPatch(nameof(HeroAgentSpawnCampaignBehavior.OnSettlementEntered))]
    static bool Prefix() => ModInformation.IsServer;
}
