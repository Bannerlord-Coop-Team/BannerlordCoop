using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

/// <summary>
/// NOT Disabled since this spawns parties and heroes at the beginning of the game
/// </summary>
[HarmonyPatch(typeof(HeroSpawnCampaignBehavior))]
internal class DisableHeroSpawnCampaignBehavior
{
    //[HarmonyPatch(nameof(HeroSpawnCampaignBehavior.RegisterEvents))]
    //static bool Prefix() => true;
}
