using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

/// <summary>
/// NOT Disabled since this spawns parties and heroes at the beginning of the game
/// </summary>
[HarmonyPatch]
internal class DisableHeroSpawnCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        return new MethodBase[]
        {
            AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "OnNonBanditClanDailyTick"),
            AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "OnHeroComesOfAge"),
            AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "OnHeroDailyTick"),
            AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "OnCompanionRemoved")
        };
    }

    [HarmonyPrefix]
    static bool Prefix() => ModInformation.IsServer;
}
