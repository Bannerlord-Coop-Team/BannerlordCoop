using Common;
using GameInterface.Services.BloodFeuds.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BloodFeuds.Patches;

[HarmonyPatch]
internal class BloodFeudClanChangePatches
{
    // Feuds belong to the clan, so a player who joins or leaves one needs that clan's feuds
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroChangedClan))]
    [HarmonyPostfix]
    private static void HeroChangedClanPostfix(Hero hero)
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return;

        executionInterface.OnHeroChangedClan(hero);
    }
}
